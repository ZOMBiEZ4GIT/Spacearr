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

    public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
    {
        var settings = await _settings.GetAsync();
        var extensions = new HashSet<string>(settings.Extensions, StringComparer.OrdinalIgnoreCase);
        var roots = await _db.RootFolders.Where(r => r.Enabled).ToListAsync(ct);
        var errors = new List<string>();

        // Phase 1: discover
        var discovered = new List<(RootFolder Root, DiscoveredFile File)>();
        for (var i = 0; i < roots.Count; i++)
        {
            ctx.Report("discover", i, roots.Count, roots[i].Path);
            if (!Directory.Exists(roots[i].Path)) { errors.Add($"Root folder not found: {roots[i].Path}"); continue; }
            foreach (var f in _discovery.Enumerate(roots[i].Path, extensions, ct)) discovered.Add((roots[i], f));
        }
        ctx.Report("discover", roots.Count, roots.Count);

        // Phase 2: decide what to probe
        var existing = await _db.MediaFiles.ToDictionaryAsync(f => PathNormalizer.Key(f.Path), ct);
        var seenKeys = new HashSet<string>();
        var toProbe = new List<(RootFolder Root, DiscoveredFile File, MediaFile? Existing)>();
        foreach (var (root, file) in discovered)
        {
            var key = PathNormalizer.Key(file.Path);
            seenKeys.Add(key);
            existing.TryGetValue(key, out var row);
            var unchanged = row is not null && row.ProbeError is null && row.SizeBytes == file.SizeBytes && Math.Abs((row.ModifiedAt - file.ModifiedAtUtc).TotalSeconds) < 2;
            if (!unchanged) toProbe.Add((root, file, row));
        }

        // Phase 3: probe in parallel, apply results on this thread
        var added = 0; var probed = 0;
        var results = new ConcurrentQueue<(RootFolder Root, DiscoveredFile File, MediaFile? Existing, ProbeResult? Result, string? Error)>();
        var total = toProbe.Count;
        ctx.Report("probe", 0, total);
        await Parallel.ForEachAsync(toProbe, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct }, async (item, token) =>
        {
            try { results.Enqueue((item.Root, item.File, item.Existing, await _prober.ProbeAsync(item.File.Path, token), null)); }
            catch (ProbeException ex) { results.Enqueue((item.Root, item.File, item.Existing, null, ex.Message)); }
            var n = Interlocked.Increment(ref probed);
            if (n % 10 == 0 || n == total) ctx.Report("probe", n, total, item.File.Path);
        });

        var now = _clock.UtcNow;
        foreach (var (root, file, existingRow, result, error) in results)
        {
            var row = existingRow;
            if (row is null) { row = new MediaFile { Path = file.Path, RootFolderId = root.Id }; _db.MediaFiles.Add(row); added++; }
            row.RootFolderId = root.Id;
            row.SizeBytes = file.SizeBytes;
            row.ModifiedAt = file.ModifiedAtUtc;
            row.ScannedAt = now;
            Apply(row, result, error);
        }
        await _db.SaveChangesAsync(ct);

        // Phase 4: remove orphans under enabled roots
        var enabledRootIds = roots.Select(r => r.Id).ToHashSet();
        var orphans = existing.Where(kv => !seenKeys.Contains(kv.Key) && enabledRootIds.Contains(kv.Value.RootFolderId)).Select(kv => kv.Value).ToList();
        _db.MediaFiles.RemoveRange(orphans);
        foreach (var r in roots) r.LastScanAt = now;
        await _db.SaveChangesAsync(ct);
        ctx.Report("cleanup", orphans.Count, orphans.Count);

        // Phase 5: enrich
        var (matched, unmatched, enrichErrors) = await _enrich.RunAsync(ctx, ct);
        errors.AddRange(enrichErrors);

        _log.LogInformation("Scan complete: {Seen} seen, {Probed} probed, {Added} added, {Removed} removed", discovered.Count, probed, added, orphans.Count);
        return new JobSummary(discovered.Count, probed, added, orphans.Count, matched, unmatched, errors.ToArray());
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
            row.VideoBitrateBps = (long)(row.SizeBytes * 8 / r.DurationSeconds.Value) - (r.AudioBitrateBps ?? 0);
    }
}
