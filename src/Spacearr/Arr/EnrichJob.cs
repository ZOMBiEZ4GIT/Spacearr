using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Scanning;

namespace Spacearr.Arr;

public sealed class EnrichJob : IJob, IEnrichRunner
{
    private readonly SpacearrDb _db;
    private readonly IArrClientFactory _factory;
    private readonly IClock _clock;
    private readonly ILogger<EnrichJob> _log;

    public EnrichJob(SpacearrDb db, IArrClientFactory factory, IClock clock, ILogger<EnrichJob> log)
    { _db = db; _factory = factory; _clock = clock; _log = log; }

    public JobType Type => JobType.Enrich;

    async Task<JobSummary> IJob.RunAsync(JobContext ctx, CancellationToken ct)
    {
        var (matched, unmatched, errors) = await RunAsync(ctx, ct);
        return new JobSummary(ItemsMatched: matched, ItemsUnmatched: unmatched, Errors: errors);
    }

    public async Task<(int Matched, int Unmatched, string[] Errors)> RunAsync(JobContext ctx, CancellationToken ct)
    {
        var instances = await _db.ArrInstances.Include(i => i.PathMappings).Where(i => i.Enabled).ToListAsync(ct);
        var filesByKey = await _db.MediaFiles.Select(f => new { f.Id, f.Path }).ToListAsync(ct);
        var fileIds = filesByKey.GroupBy(f => PathNormalizer.Key(f.Path)).ToDictionary(g => g.Key, g => g.First().Id);

        int matched = 0, unmatched = 0;
        var errors = new List<string>();
        for (var n = 0; n < instances.Count; n++)
        {
            var inst = instances[n];
            ctx.Report("enrich", n, instances.Count, inst.Name);
            var now = _clock.UtcNow;
            try
            {
                // Fetch everything from the arr instance up front, before any _db
                // mutation. This keeps an ArrException (e.g. GetProfilesAsync
                // failing after items were already fetched) from ever landing
                // after tracked entities were added/modified - otherwise the
                // catch block's SaveChangesAsync below would persist a
                // half-applied upsert while still reporting the instance failed.
                var client = _factory.Create(inst);
                var tags = (await client.GetTagsAsync(ct)).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Label);
                var items = await client.GetItemsAsync(ct);
                var profiles = (await client.GetProfilesAsync(ct)).GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First().Name);

                var mapper = new PathMapper(inst.PathMappings);
                var existing = await _db.MediaItems.Where(i => i.ArrInstanceId == inst.Id).ToDictionaryAsync(i => (i.Kind, i.ExternalId), ct);
                var seen = new HashSet<(MediaKind, int)>();

                foreach (var item in items)
                {
                    var key = (item.Kind, item.ExternalId);
                    seen.Add(key);
                    if (!existing.TryGetValue(key, out var row))
                    {
                        row = new MediaItem { ArrInstanceId = inst.Id, Kind = item.Kind, ExternalId = item.ExternalId };
                        _db.MediaItems.Add(row);
                        existing[key] = row;
                    }
                    row.Title = item.Title; row.Year = item.Year;
                    row.SeriesId = item.SeriesId; row.SeriesTitle = item.SeriesTitle; row.SeasonNumber = item.SeasonNumber;
                    row.EpisodeNumbers = item.EpisodeNumbers.Length == 0 ? null : string.Join(',', item.EpisodeNumbers);
                    row.EpisodeIds = item.EpisodeIds.Length == 0 ? null : string.Join(',', item.EpisodeIds);
                    row.QualityProfileId = item.QualityProfileId; row.QualityName = item.QualityName;
                    row.QualityProfileName = item.QualityProfileId is int pid && profiles.TryGetValue(pid, out var pname) ? pname : null;
                    row.Monitored = item.Monitored;
                    row.Tags = item.TagIds.Length == 0 ? null : string.Join(',', item.TagIds.Select(id => tags.TryGetValue(id, out var l) ? l : id.ToString()));
                    row.PosterUrl = item.PosterUrl; row.TmdbId = item.TmdbId; row.TvdbId = item.TvdbId; row.ImdbId = item.ImdbId;
                    row.ArrFileId = item.ArrFileId; row.ArrPath = item.ArrPath; row.SyncedAt = now;

                    var local = item.ArrPath is null ? null : mapper.Map(item.ArrPath);
                    if (local is not null && fileIds.TryGetValue(PathNormalizer.Key(local), out var fileId)) { row.MediaFileId = fileId; matched++; }
                    else { row.MediaFileId = null; unmatched++; }
                }

                var stale = existing.Where(kv => !seen.Contains(kv.Key)).Select(kv => kv.Value).ToList();
                _db.MediaItems.RemoveRange(stale);

                // FindAsync returns the already-tracked instance if one earlier
                // iteration hasn't cleared the tracker (the common case), or
                // re-queries it if a prior instance's failure did - see the
                // ChangeTracker.Clear() below.
                var trackedInst = await _db.ArrInstances.FindAsync(new object[] { inst.Id }, ct) ?? inst;
                trackedInst.LastSyncAt = now; trackedInst.LastSyncError = null;
                await _db.SaveChangesAsync(ct);
                _log.LogInformation("Enriched {Instance}: {Items} items, {Stale} removed", inst.Name, items.Count, stale.Count);
            }
            // Any failure for one instance (an ArrException, but also anything
            // unexpected) is recorded against that instance and the loop moves on:
            // one broken connection must never abort the sync of the others.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{inst.Name}: {ex.Message}");
                _log.LogWarning("Enrich failed for {Instance}: {Message}", inst.Name, ex.Message);

                // Belt-and-braces: drop any partially-tracked state from this
                // iteration before recording the failure (there should be none,
                // now that all arr calls happen before any _db mutation above,
                // but this keeps a future change from silently reintroducing the
                // hazard). Clear() detaches every tracked entity, so the instance
                // must be re-queried rather than mutated via the now-detached
                // `inst` reference.
                _db.ChangeTracker.Clear();
                try
                {
                    var freshInst = await _db.ArrInstances.FindAsync(new object[] { inst.Id }, ct);
                    if (freshInst is not null) freshInst.LastSyncError = ex.Message;
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception saveEx) when (saveEx is not OperationCanceledException)
                {
                    _log.LogWarning(saveEx, "Could not record sync error for {Instance}", inst.Name);
                    errors.Add($"{inst.Name}: could not record sync error: {saveEx.Message}");
                    continue;
                }
            }
        }
        ctx.Report("enrich", instances.Count, instances.Count);
        return (matched, unmatched, errors.ToArray());
    }
}
