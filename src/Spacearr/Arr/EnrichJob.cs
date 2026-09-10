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
            try
            {
                var client = _factory.Create(inst);
                var tags = (await client.GetTagsAsync(ct)).ToDictionary(t => t.Id, t => t.Label);
                var items = await client.GetItemsAsync(ct);
                var mapper = new PathMapper(inst.PathMappings);
                var existing = await _db.MediaItems.Where(i => i.ArrInstanceId == inst.Id).ToDictionaryAsync(i => (i.Kind, i.ExternalId), ct);
                var seen = new HashSet<(MediaKind, int)>();
                var now = _clock.UtcNow;

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
                    row.QualityProfileId = item.QualityProfileId; row.QualityName = item.QualityName;
                    row.Monitored = item.Monitored;
                    row.Tags = item.TagIds.Length == 0 ? null : string.Join(',', item.TagIds.Select(id => tags.TryGetValue(id, out var l) ? l : id.ToString()));
                    row.PosterUrl = item.PosterUrl; row.TmdbId = item.TmdbId; row.TvdbId = item.TvdbId; row.ImdbId = item.ImdbId;
                    row.ArrFileId = item.ArrFileId; row.ArrPath = item.ArrPath; row.SyncedAt = now;
                    row.QualityProfileName = null; // filled below from profiles

                    var local = item.ArrPath is null ? null : mapper.Map(item.ArrPath);
                    if (local is not null && fileIds.TryGetValue(PathNormalizer.Key(local), out var fileId)) { row.MediaFileId = fileId; matched++; }
                    else { row.MediaFileId = null; unmatched++; }
                }

                var profiles = (await client.GetProfilesAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
                foreach (var row in existing.Values) if (row.QualityProfileId is int pid && profiles.TryGetValue(pid, out var pname)) row.QualityProfileName = pname;

                var stale = existing.Where(kv => !seen.Contains(kv.Key)).Select(kv => kv.Value).ToList();
                _db.MediaItems.RemoveRange(stale);
                inst.LastSyncAt = now; inst.LastSyncError = null;
                await _db.SaveChangesAsync(ct);
                _log.LogInformation("Enriched {Instance}: {Items} items, {Stale} removed", inst.Name, items.Count, stale.Count);
            }
            catch (ArrException ex)
            {
                inst.LastSyncError = ex.Message;
                await _db.SaveChangesAsync(ct);
                errors.Add($"{inst.Name}: {ex.Message}");
                _log.LogWarning("Enrich failed for {Instance}: {Message}", inst.Name, ex.Message);
            }
        }
        ctx.Report("enrich", instances.Count, instances.Count);
        return (matched, unmatched, errors.ToArray());
    }
}
