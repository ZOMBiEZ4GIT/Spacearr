using Microsoft.EntityFrameworkCore;
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
    private readonly IClock _clock;
    private readonly ILogger<ActionJob> _log;

    public ActionJob(ActionRequest request, SpacearrDb db, IArrClientFactory factory, IClock clock, ILogger<ActionJob> log)
    { _request = request; _db = db; _factory = factory; _clock = clock; _log = log; }

    public JobType Type => JobType.Action;

    public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
    {
        var item = await _db.MediaItems.Include(i => i.ArrInstance).Include(i => i.MediaFile).SingleOrDefaultAsync(i => i.Id == _request.ItemId, ct)
                   ?? throw new InvalidOperationException("Item disappeared before the action ran.");
        var inst = item.ArrInstance!;
        var client = _factory.Create(inst);
        var completed = new List<string>();
        var log = new ActionLog
        {
            At = _clock.UtcNow, Type = _request.Type, MediaItemId = item.Id, ArrInstanceId = inst.Id,
            Title = ActionPlanner.TitleFor(item), Path = item.MediaFile?.Path, SizeBytesBefore = item.MediaFile?.SizeBytes ?? 0,
            QualityBefore = item.QualityProfileName,
        };
        try
        {
            var total = _request.Type == ActionType.Replace ? 3 : (_request.Unmonitor ? 2 : 1);
            var n = 0;
            if (_request.Type == ActionType.Replace)
            {
                var targetId = inst.Type == ArrType.Radarr ? item.ExternalId : item.SeriesId!.Value;
                await client.SetProfileAsync(targetId, _request.TargetProfileId!.Value, ct);
                var profiles = await client.GetProfilesAsync(ct);
                log.QualityAfter = profiles.FirstOrDefault(p => p.Id == _request.TargetProfileId)?.Name;
                completed.Add("profile changed"); ctx.Report("action", ++n, total, "profile changed");
                await client.DeleteFileAsync(item.ArrFileId!.Value, ct);
                completed.Add("file deleted"); ctx.Report("action", ++n, total, "file deleted");
                var searchIds = inst.Type == ArrType.Radarr ? new[] { item.ExternalId } : EpisodeIds(item);
                await client.SearchAsync(searchIds, ct);
                completed.Add("search requested"); ctx.Report("action", ++n, total, "search requested");
            }
            else
            {
                await client.DeleteFileAsync(item.ArrFileId!.Value, ct);
                completed.Add("file deleted"); ctx.Report("action", ++n, total, "file deleted");
                if (_request.Unmonitor)
                {
                    if (inst.Type == ArrType.Radarr) await client.UnmonitorAsync(item.ExternalId, ct);
                    else foreach (var id in EpisodeIds(item)) await client.UnmonitorAsync(id, ct);
                    completed.Add("unmonitored"); ctx.Report("action", ++n, total, "unmonitored");
                }
            }
            log.Outcome = ActionOutcome.Succeeded;
            log.Detail = string.Join(", ", completed);
            if (item.MediaFile is not null) _db.MediaFiles.Remove(item.MediaFile);
            item.MediaFileId = null; item.ArrFileId = null;
        }
        catch (ArrException ex)
        {
            log.Outcome = ActionOutcome.Failed;
            log.Detail = $"{ex.Message}. Completed before failure: {(completed.Count == 0 ? "nothing" : string.Join(", ", completed))}.";
            _log.LogWarning("Action failed for {Title}: {Message}", log.Title, ex.Message);
        }
        _db.ActionLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        if (log.Outcome == ActionOutcome.Failed) throw new InvalidOperationException(log.Detail);
        return new JobSummary();
    }

    // Sonarr search and unmonitor need episode ids; EnrichJob stores them on MediaItem.EpisodeIds (Step 6).
    private static int[] EpisodeIds(MediaItem item) =>
        (item.EpisodeIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

}
