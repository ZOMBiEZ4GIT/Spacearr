using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Spacearr.Arr;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Library;

namespace Spacearr.Actions;

public sealed class ActionJob : IJob
{
    private readonly ActionRequest _request;
    private readonly SpacearrDb _db;
    private readonly IArrClientFactory _factory;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<ActionJob> _log;

    public ActionJob(ActionRequest request, SpacearrDb db, IArrClientFactory factory, IMemoryCache cache, IClock clock, ILogger<ActionJob> log)
    { _request = request; _db = db; _factory = factory; _cache = cache; _clock = clock; _log = log; }

    public JobType Type => JobType.Action;

    public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
    {
        var item = await _db.MediaItems.Include(i => i.ArrInstance).Include(i => i.MediaFile).SingleOrDefaultAsync(i => i.Id == _request.ItemId, ct)
                   ?? throw new InvalidOperationException("Item disappeared before the action ran.");
        var inst = item.ArrInstance!;
        var completed = new List<string>();
        var log = new ActionLog
        {
            At = _clock.UtcNow, Type = _request.Type, MediaItemId = item.Id, ArrInstanceId = inst.Id,
            Title = ActionPlanner.TitleFor(item), Path = item.MediaFile?.Path, SizeBytesBefore = item.MediaFile?.SizeBytes ?? 0,
            QualityBefore = item.QualityProfileName,
        };

        // Guard against a repeated/late-arriving execute (e.g. the item was already
        // actioned by a prior job, or the file vanished from under us) so we never
        // dereference a null ArrFileId/MediaFile below - and still write an audit row.
        if (item.MediaFile is null || item.ArrFileId is null)
        {
            log.Outcome = ActionOutcome.Failed;
            log.Detail = "The file is no longer known to the arr app (already actioned?)";
            _db.ActionLogs.Add(log);
            await _db.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException(log.Detail);
        }

        try
        {
            var client = _factory.Create(inst);
            var total = _request.Type == ActionType.Replace ? 3 : (_request.Unmonitor ? 2 : 1);
            var n = 0;
            if (_request.Type == ActionType.Replace)
            {
                if (inst.Type == ArrType.Sonarr && item.SeriesId is null)
                    throw new InvalidOperationException("This episode's series is unknown; run a scan and try again.");

                var targetId = inst.Type == ArrType.Radarr ? item.ExternalId : item.SeriesId!.Value;
                await client.SetProfileAsync(targetId, _request.TargetProfileId!.Value, ct);
                completed.Add("profile changed"); ctx.Report("action", ++n, total, "profile changed");

                // Resolve the human-readable profile name from the planner's cached
                // profile list rather than another live call to the arr app in the
                // middle of this critical sequence; fall back to the raw id.
                var cachedProfiles = _cache.Get<IReadOnlyList<ArrProfile>>($"profiles:{inst.Id}");
                log.QualityAfter = cachedProfiles?.FirstOrDefault(p => p.Id == _request.TargetProfileId)?.Name
                                    ?? _request.TargetProfileId!.Value.ToString();
                item.QualityProfileId = _request.TargetProfileId;
                item.QualityProfileName = log.QualityAfter;

                await client.DeleteFileAsync(item.ArrFileId!.Value, ct);
                completed.Add("file deleted"); ctx.Report("action", ++n, total, "file deleted");
                RemoveFile(item);

                var searchIds = inst.Type == ArrType.Radarr ? new[] { item.ExternalId } : EpisodeIds(item);
                if (inst.Type == ArrType.Sonarr && searchIds.Length == 0)
                {
                    completed.Add("no episode ids known; search skipped"); ctx.Report("action", ++n, total, "search skipped");
                }
                else
                {
                    await client.SearchAsync(searchIds, ct);
                    completed.Add("search requested"); ctx.Report("action", ++n, total, "search requested");
                }
            }
            else
            {
                await client.DeleteFileAsync(item.ArrFileId!.Value, ct);
                completed.Add("file deleted"); ctx.Report("action", ++n, total, "file deleted");
                RemoveFile(item);

                if (_request.Unmonitor)
                {
                    if (inst.Type == ArrType.Radarr) await client.UnmonitorAsync(item.ExternalId, ct);
                    else foreach (var id in EpisodeIds(item)) await client.UnmonitorAsync(id, ct);
                    completed.Add("unmonitored"); ctx.Report("action", ++n, total, "unmonitored");
                }
            }
            log.Outcome = ActionOutcome.Succeeded;
            log.Detail = string.Join(", ", completed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Outcome = ActionOutcome.Failed;
            log.Detail = $"{ex.Message}. Completed before failure: {(completed.Count == 0 ? "nothing" : string.Join(", ", completed))}.";
            _log.LogWarning(ex, "Action failed for {Title}: {Message}", log.Title, ex.Message);
        }
        _db.ActionLogs.Add(log);
        await _db.SaveChangesAsync(CancellationToken.None);
        if (log.Outcome == ActionOutcome.Failed) throw new InvalidOperationException(log.Detail);
        return new JobSummary();
    }

    /// <summary>
    /// Drops the MediaFile row and clears the item's file pointers immediately once
    /// the arr-side delete has actually succeeded, rather than waiting until every
    /// later step (search/unmonitor) also succeeds - a failure after this point must
    /// not leave a MediaFile row referencing a file the arr app has already deleted.
    /// </summary>
    private void RemoveFile(MediaItem item)
    {
        if (item.MediaFile is not null) { _db.MediaFiles.Remove(item.MediaFile); item.MediaFile = null; }
        item.MediaFileId = null;
        item.ArrFileId = null;
    }

    // Sonarr search and unmonitor need episode ids; EnrichJob stores them on MediaItem.EpisodeIds (Step 6).
    // Malformed entries (should never happen, but this column is a plain string) are
    // skipped rather than throwing, so one bad id doesn't abort the whole action.
    private static int[] EpisodeIds(MediaItem item) =>
        (item.EpisodeIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var v) ? (int?)v : null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
}
