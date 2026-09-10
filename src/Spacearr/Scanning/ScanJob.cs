using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Settings;

namespace Spacearr.Scanning;

public interface IEnrichRunner
{
    Task<(int Matched, int Unmatched, string[] Errors)> RunAsync(JobContext ctx, CancellationToken ct);
}

public sealed class NoEnrichRunner : IEnrichRunner
{
    public Task<(int Matched, int Unmatched, string[] Errors)> RunAsync(JobContext ctx, CancellationToken ct) =>
        Task.FromResult((0, 0, Array.Empty<string>()));
}

public sealed class ScanJob : IJob
{
    private const int Parallelism = 4;
    private const int DeleteChunkSize = 500;
    private readonly SpacearrDb _db;
    private readonly IFileDiscovery _discovery;
    private readonly IMediaProber _prober;
    private readonly ISettingsService _settings;
    private readonly IClock _clock;
    private readonly IEnrichRunner _enrich;
    private readonly ILogger<ScanJob> _log;

    public ScanJob(SpacearrDb db, IFileDiscovery discovery, IMediaProber prober, ISettingsService settings, IClock clock, IEnrichRunner enrich, ILogger<ScanJob> log)
    { _db = db; _discovery = discovery; _prober = prober; _settings = settings; _clock = clock; _enrich = enrich; _log = log; }

    public JobType Type => JobType.Scan;

    private sealed record ExistingSnapshot(int Id, string Path, int RootFolderId, long SizeBytes, DateTime ModifiedAt, string? ProbeError);

    public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
    {
        var settings = await _settings.GetAsync();
        var extensions = new HashSet<string>(settings.Extensions, StringComparer.OrdinalIgnoreCase);
        var roots = await _db.RootFolders.Where(r => r.Enabled).ToListAsync(ct);
        var errors = new List<string>();

        // Phase 1: discover. Only roots that actually existed this run go into
        // enumeratedRootIds - a root whose directory is temporarily missing (an
        // unmounted NAS, say) must never be treated as "scanned empty", or phase 4
        // below would delete every MediaFile row that lives under it.
        var rawDiscovered = new List<(RootFolder Root, DiscoveredFile File)>();
        var enumeratedRootIds = new HashSet<int>();
        for (var i = 0; i < roots.Count; i++)
        {
            ctx.Report("discover", i, roots.Count, roots[i].Path);
            if (!Directory.Exists(roots[i].Path)) { errors.Add($"Root folder not found: {roots[i].Path}"); continue; }
            enumeratedRootIds.Add(roots[i].Id);
            foreach (var f in _discovery.Enumerate(roots[i].Path, extensions, ct)) rawDiscovered.Add((roots[i], f));
        }
        ctx.Report("discover", roots.Count, roots.Count);

        // Overlapping/nested roots (e.g. <tmp> and <tmp>/sub) can discover the same
        // file twice, once per root. MediaFile.Path is unique, so a duplicate would
        // crash the upsert; keep only the first root a given file is seen under.
        var discovered = new List<(RootFolder Root, DiscoveredFile File)>();
        var dedupeKeys = new HashSet<string>();
        foreach (var pair in rawDiscovered)
        {
            if (dedupeKeys.Add(PathNormalizer.Key(pair.File.Path))) discovered.Add(pair);
        }

        // Phase 2: decide what to probe. Loaded no-tracking and grouped by key so a
        // stray case-variant duplicate path can't blow up ToDictionary, and so this
        // pass doesn't put the whole MediaFiles table under change tracking.
        var existingByKey = (await _db.MediaFiles
                .AsNoTracking()
                .Select(f => new ExistingSnapshot(f.Id, f.Path, f.RootFolderId, f.SizeBytes, f.ModifiedAt, f.ProbeError))
                .ToListAsync(ct))
            .GroupBy(f => PathNormalizer.Key(f.Path))
            .ToDictionary(g => g.Key, g => g.First());

        var seenKeys = new HashSet<string>();
        var toProbe = new List<(RootFolder Root, DiscoveredFile File, ExistingSnapshot? Existing)>();
        foreach (var (root, file) in discovered)
        {
            var key = PathNormalizer.Key(file.Path);
            seenKeys.Add(key);
            existingByKey.TryGetValue(key, out var row);
            var unchanged = row is not null && row.ProbeError is null && row.SizeBytes == file.SizeBytes && Math.Abs((row.ModifiedAt - file.ModifiedAtUtc).TotalSeconds) < 2;
            if (!unchanged) toProbe.Add((root, file, row));
        }

        // Phase 3: probe in parallel, apply results on this thread. A failing probe -
        // whether the expected ProbeException or anything else the prober or process
        // plumbing throws - must only taint that one file, never abort the scan; the
        // `when` guard keeps cancellation propagating instead of being swallowed here.
        var probed = 0;
        var results = new ConcurrentQueue<(RootFolder Root, DiscoveredFile File, ExistingSnapshot? Existing, ProbeResult? Result, string? Error)>();
        var total = toProbe.Count;
        ctx.Report("probe", 0, total);
        await Parallel.ForEachAsync(toProbe, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct }, async (item, token) =>
        {
            try { results.Enqueue((item.Root, item.File, item.Existing, await _prober.ProbeAsync(item.File.Path, token), null)); }
            catch (Exception ex) when (ex is not OperationCanceledException) { results.Enqueue((item.Root, item.File, item.Existing, null, ex.Message)); }
            var n = Interlocked.Increment(ref probed);
            if (n % 10 == 0 || n == total) ctx.Report("probe", n, total, item.File.Path);
        });

        // Load tracked entities only for the rows that will actually be written
        // (existing rows that changed), instead of change-tracking everything.
        var idsToLoad = results.Where(r => r.Existing is not null).Select(r => r.Existing!.Id).ToHashSet();
        var trackedExisting = idsToLoad.Count == 0
            ? new Dictionary<int, MediaFile>()
            : await _db.MediaFiles.Where(f => idsToLoad.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var now = _clock.UtcNow;
        var added = 0;
        foreach (var (root, file, existingRow, result, error) in results)
        {
            MediaFile row;
            if (existingRow is null) { row = new MediaFile { Path = file.Path, RootFolderId = root.Id }; _db.MediaFiles.Add(row); added++; }
            else row = trackedExisting[existingRow.Id];
            row.Path = file.Path; // keep casing changes on an existing row in sync
            row.RootFolderId = root.Id;
            row.SizeBytes = file.SizeBytes;
            row.ModifiedAt = file.ModifiedAtUtc;
            row.ScannedAt = now;
            Apply(row, result, error);
        }
        await _db.SaveChangesAsync(ct);

        // Phase 4: remove orphans, but only under roots that were actually
        // enumerated this run (see the enumeratedRootIds comment above), and via
        // ExecuteDeleteAsync so this never has to load the rows into the tracker.
        var orphanIds = existingByKey
            .Where(kv => !seenKeys.Contains(kv.Key) && enumeratedRootIds.Contains(kv.Value.RootFolderId))
            .Select(kv => kv.Value.Id)
            .ToList();
        var removed = 0;
        foreach (var chunk in orphanIds.Chunk(DeleteChunkSize))
            removed += await _db.MediaFiles.Where(f => chunk.Contains(f.Id)).ExecuteDeleteAsync(ct);
        ctx.Report("cleanup", removed, removed);

        // Only stamp LastScanAt for roots that were actually scanned this run - a
        // missing root should not look like it was just (successfully) scanned.
        foreach (var r in roots.Where(r => enumeratedRootIds.Contains(r.Id))) r.LastScanAt = now;
        await _db.SaveChangesAsync(ct);

        // Phase 5: enrich
        var (matched, unmatched, enrichErrors) = await _enrich.RunAsync(ctx, ct);
        errors.AddRange(enrichErrors);

        _log.LogInformation("Scan complete: {Seen} seen, {Probed} probed, {Added} added, {Removed} removed", discovered.Count, probed, added, removed);
        return new JobSummary(discovered.Count, probed, added, removed, matched, unmatched, errors.ToArray());
    }

    private static void Apply(MediaFile row, ProbeResult? r, string? error)
    {
        row.ProbeError = error;
        row.DurationSeconds = r?.DurationSeconds;
        row.Width = r?.Width; row.Height = r?.Height; row.FrameRate = r?.FrameRate;
        row.VideoCodec = r?.VideoCodec; row.VideoProfile = r?.VideoProfile; row.BitDepth = r?.BitDepth;
        row.OverallBitrateBps = r?.OverallBitrateBps; row.VideoBitrateBps = r?.VideoBitrateBps;
        row.Container = r?.Container; row.AudioSummary = r?.AudioSummary; row.AudioBitrateBps = r?.AudioBitrateBps;
        row.HdrFormat = r?.HdrFormat;
        if (r is not null && r.VideoBitrateBps is null && r.DurationSeconds is > 0)
            row.VideoBitrateBps = Math.Max(0, (long)(row.SizeBytes * 8 / r.DurationSeconds.Value) - (r.AudioBitrateBps ?? 0));
    }
}
