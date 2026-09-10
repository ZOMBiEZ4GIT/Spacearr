using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Spacearr.Arr;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Library;
using Spacearr.Settings;

namespace Spacearr.Actions;

public sealed class ActionPlanner
{
    private readonly SpacearrDb _db;
    private readonly IArrClientFactory _factory;
    private readonly IMemoryCache _cache;
    private readonly ISettingsService _settings;
    private readonly ConfirmTokens _tokens;

    public ActionPlanner(SpacearrDb db, IArrClientFactory factory, IMemoryCache cache, ISettingsService settings, ConfirmTokens tokens)
    { _db = db; _factory = factory; _cache = cache; _settings = settings; _tokens = tokens; }

    public async Task<ActionPreview> PlanAsync(ActionRequest req, CancellationToken ct)
    {
        var item = await _db.MediaItems.Include(i => i.ArrInstance).Include(i => i.MediaFile).SingleOrDefaultAsync(i => i.Id == req.ItemId, ct)
                   ?? throw new ActionPlanException("That title is no longer in the library. Run a scan and try again.");
        if (item.MediaFile is null || item.ArrFileId is null) throw new ActionPlanException("This title has no file known to the arr app, so there is nothing to act on.");
        var inst = item.ArrInstance!;
        var steps = new List<ActionStep>();
        string? warning = null;
        SavingsEstimate? estimate = null;
        string? targetName = null;

        if (req.Type == ActionType.Replace)
        {
            if (req.TargetProfileId is null) throw new ActionPlanException("Choose a target quality profile.");
            if (req.TargetProfileId == item.QualityProfileId) throw new ActionPlanException("That is already the current quality profile.");
            var profiles = await _cache.GetOrCreateAsync($"profiles:{inst.Id}", async e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); return await _factory.Create(inst).GetProfilesAsync(ct); });
            var target = profiles!.FirstOrDefault(p => p.Id == req.TargetProfileId) ?? throw new ActionPlanException("That quality profile does not exist on the arr app.");
            targetName = target.Name;
            var (rows, _) = await LibraryEndpoints.Load(_db, _settings, new LibraryFilter(null, null, 0, null), null, ct);
            var row = rows.FirstOrDefault(r => r.ItemId == item.Id);
            if (row is not null) estimate = SavingsEstimator.Estimate(row, target.Name, rows);

            if (inst.Type == ArrType.Radarr)
            {
                steps.Add(new ActionStep($"Set quality profile to {target.Name} on Radarr", "PUT", $"/api/v3/movie/{item.ExternalId}"));
                steps.Add(new ActionStep("Delete the current file through Radarr", "DELETE", $"/api/v3/moviefile/{item.ArrFileId}"));
                steps.Add(new ActionStep("Ask Radarr to search for a replacement", "POST", "/api/v3/command"));
            }
            else
            {
                warning = $"This changes the quality profile for the whole series '{item.SeriesTitle}', not just this episode.";
                steps.Add(new ActionStep($"Set quality profile to {target.Name} on the series in Sonarr", "PUT", $"/api/v3/series/{item.SeriesId}"));
                steps.Add(new ActionStep("Delete the current file through Sonarr", "DELETE", $"/api/v3/episodefile/{item.ArrFileId}"));
                steps.Add(new ActionStep("Ask Sonarr to search for a replacement", "POST", "/api/v3/command"));
            }
        }
        else
        {
            if (inst.Type == ArrType.Radarr)
            {
                steps.Add(new ActionStep("Delete the file through Radarr", "DELETE", $"/api/v3/moviefile/{item.ArrFileId}"));
                if (req.Unmonitor) steps.Add(new ActionStep("Unmonitor the movie so Radarr does not re-download it", "PUT", $"/api/v3/movie/{item.ExternalId}"));
            }
            else
            {
                steps.Add(new ActionStep("Delete the file through Sonarr", "DELETE", $"/api/v3/episodefile/{item.ArrFileId}"));
                if (req.Unmonitor)
                {
                    warning = "All episodes on this file will be unmonitored.";
                    steps.Add(new ActionStep("Unmonitor the episode(s) so Sonarr does not re-download them", "PUT", "/api/v3/episode/{id}"));
                }
            }
        }

        var clean = req with { ConfirmToken = null };
        var token = _tokens.Issue(clean);
        return new ActionPreview(clean, TitleFor(item), inst.Name, inst.Type, item.MediaFile.SizeBytes, estimate, warning, steps.ToArray(), token, _tokens.ExpiryFor(token));
    }

    public static string TitleFor(MediaItem i) => i.Kind == MediaKind.Movie
        ? (i.Year is null ? i.Title : $"{i.Title} ({i.Year})")
        : $"{i.SeriesTitle} S{i.SeasonNumber:00}E{string.Join("-E", (i.EpisodeNumbers ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(e => int.TryParse(e, out var n) ? n.ToString("00") : e))}";
}
