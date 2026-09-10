using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Spacearr.Arr;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Jobs;
using Spacearr.Settings;

namespace Spacearr.Library;

public sealed record LibraryItemResponse(
    int ItemId, int InstanceId, string InstanceName, ArrType InstanceType, MediaKind Kind, string Title, int? Year,
    int? SeriesId, string? SeriesTitle, int? SeasonNumber, string? Episodes,
    string? QualityProfileName, int? QualityProfileId, string? QualityName, bool Monitored, string? Tags, string? PosterUrl,
    int FileId, string Path, long SizeBytes, double? DurationSeconds, int? Width, int? Height, double? FrameRate,
    string? VideoCodec, int? BitDepth, string? HdrFormat, long? VideoBitrateBps, long? OverallBitrateBps, string? AudioSummary, string? ProbeError,
    double? Nbpp, string? Resolution, double Heat, string Color)
{
    public static LibraryItemResponse From(LibraryRow r, double heat) => new(
        r.ItemId, r.InstanceId, r.InstanceName, r.InstanceType, r.Kind, r.Title, r.Year, r.SeriesId, r.SeriesTitle, r.SeasonNumber, r.Episodes,
        r.QualityProfileName, r.QualityProfileId, r.QualityName, r.Monitored, r.Tags, r.PosterUrl, r.FileId, r.Path, r.SizeBytes, r.DurationSeconds,
        r.Width, r.Height, r.FrameRate, r.VideoCodec, r.BitDepth, r.HdrFormat, r.VideoBitrateBps, r.OverallBitrateBps, r.AudioSummary, r.ProbeError,
        r.Nbpp, r.Resolution, double.IsNaN(heat) ? -1 : heat, Spacearr.Library.Heat.Color(heat));
}

public sealed record ProfileEstimate(int Id, string Name, SavingsEstimate Estimate);
public sealed record LibraryDetailResponse(LibraryItemResponse Item, ProfileEstimate[] Profiles);
public sealed record Bucket(string Name, long Bytes, int Count);
public sealed record InstanceBucket(int Id, string Name, ArrType Type, long Bytes, int Count);
public sealed record LibraryStats(long TotalBytes, int FileCount, int ItemCount, int UnmatchedFileCount, int UnreadableFileCount,
    InstanceBucket[] ByInstance, Bucket[] ByQuality, Bucket[] ByCodec, Bucket[] ByResolution, int[] HeatHistogram,
    LibraryItemResponse[] Largest, LibraryItemResponse[] Hottest);

public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/library").RequireAuthorization();

        group.MapGet("/", async (SpacearrDb db, ISettingsService settings, CancellationToken ct,
            int? instanceId, MediaKind? kind, long minBytes = 0, string? search = null, string sort = "size", string order = "desc", int page = 1, int pageSize = 100, string? heatMode = null) =>
        {
            var (rows, heat) = await Load(db, settings, new LibraryFilter(instanceId, kind, minBytes, search), heatMode, ct);
            var scored = rows.Select((r, i) => LibraryItemResponse.From(r, heat[i]));
            scored = sort switch
            {
                "heat" => scored.OrderBy(x => x.Heat),
                "title" => scored.OrderBy(x => x.SeriesTitle ?? x.Title).ThenBy(x => x.Title),
                "quality" => scored.OrderBy(x => x.QualityName),
                _ => scored.OrderBy(x => x.SizeBytes),
            };
            if (order != "asc") scored = scored.Reverse();
            var list = scored.ToList();
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 1000);
            // Clamp page to the last real page before computing skip: an unclamped huge
            // page (e.g. 2_000_000_000) times pageSize can overflow int, and Skip takes int.
            var maxPage = Math.Max(1, (int)Math.Ceiling(list.Count / (double)pageSize));
            if (page > maxPage) page = maxPage;
            var skip = (long)(page - 1) * pageSize;
            var items = list.Skip((int)Math.Min(skip, list.Count)).Take(pageSize).ToArray();
            return Results.Ok(new PageResponse<LibraryItemResponse>(items, list.Count, page, pageSize));
        });

        group.MapGet("/tree", async (SpacearrDb db, ISettingsService settings, CancellationToken ct,
            int? instanceId, MediaKind? kind, long minBytes = 0, string colorBy = "heat", string? heatMode = null) =>
        {
            var (rows, heat) = await Load(db, settings, new LibraryFilter(instanceId, kind, 0, null), heatMode, ct);
            ISet<int>? dups = colorBy == "duplicates" ? DuplicateFinder.Find(rows).SelectMany(g => g.Members).Select(m => m.FileId).ToHashSet() : null;
            var total = rows.Sum(r => r.SizeBytes);
            var fold = Math.Max(minBytes, total / 4000); // never more than ~4000 visible leaves at the top level
            return Results.Ok(TreeBuilder.Build(rows, heat, colorBy, fold, 10000, dups));
        });

        group.MapGet("/stats", async (SpacearrDb db, ISettingsService settings, CancellationToken ct, int? instanceId, MediaKind? kind, string? heatMode = null) =>
        {
            var (rows, heat) = await Load(db, settings, new LibraryFilter(instanceId, kind, 0, null), heatMode, ct);
            var scored = rows.Select((r, i) => LibraryItemResponse.From(r, heat[i])).ToList();
            Bucket[] By(Func<LibraryRow, string?> key) => rows.GroupBy(r => key(r) ?? "Unknown").Select(g => new Bucket(g.Key, g.Sum(r => r.SizeBytes), g.Count())).OrderByDescending(b => b.Bytes).ToArray();
            var histogram = new int[10];
            foreach (var h in heat.Where(h => !double.IsNaN(h))) histogram[Math.Min(9, (int)(h * 10))]++;
            return Results.Ok(new LibraryStats(
                rows.Sum(r => r.SizeBytes), rows.Count, rows.Count(r => r.ItemId != 0), rows.Count(r => r.ItemId == 0), rows.Count(r => r.ProbeError != null),
                rows.GroupBy(r => (r.InstanceId, r.InstanceName, r.InstanceType)).Select(g => new InstanceBucket(g.Key.InstanceId, g.Key.InstanceName, g.Key.InstanceType, g.Sum(r => r.SizeBytes), g.Count())).OrderByDescending(b => b.Bytes).ToArray(),
                By(r => r.QualityName), By(r => r.VideoCodec), By(r => r.Resolution), histogram,
                scored.OrderByDescending(x => x.SizeBytes).Take(5).ToArray(),
                scored.Where(x => x.Heat >= 0).OrderByDescending(x => x.Heat).ThenByDescending(x => x.SizeBytes).Take(5).ToArray()));
        });

        group.MapGet("/{itemId:int}", async (int itemId, SpacearrDb db, ISettingsService settings, IArrClientFactory factory, IMemoryCache cache, CancellationToken ct) =>
        {
            // ItemId 0 (and negative ids) mean "unmatched loose file" in LibraryRow, not a
            // real item - without this guard /library/0 would return whichever unmatched
            // row happens to be first instead of a 404.
            if (itemId <= 0) return Results.NotFound();
            var (rows, heat) = await Load(db, settings, new LibraryFilter(null, null, 0, null), null, ct);
            var index = rows.FindIndex(r => r.ItemId == itemId);
            if (index < 0) return Results.NotFound();
            var row = rows[index];
            var item = LibraryItemResponse.From(row, heat[index]);
            var inst = await db.ArrInstances.FindAsync(new object[] { row.InstanceId }, ct);
            var profiles = Array.Empty<ProfileEstimate>();
            if (inst is not null)
            {
                try
                {
                    var list = await cache.GetOrCreateAsync($"profiles:{inst.Id}", async e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); return await factory.Create(inst).GetProfilesAsync(ct); });
                    profiles = list!.Where(p => p.Id != row.QualityProfileId).Select(p => new ProfileEstimate(p.Id, p.Name, SavingsEstimator.Estimate(row, p.Name, rows))).ToArray();
                }
                catch (ArrException) { /* profiles unavailable; detail still renders */ }
            }
            return Results.Ok(new LibraryDetailResponse(item, profiles));
        });

        return app;
    }

    internal static async Task<(List<LibraryRow> Rows, double[] Heat)> Load(SpacearrDb db, ISettingsService settings, LibraryFilter filter, string? heatMode, CancellationToken ct)
    {
        var rows = await LibraryQueries.RowsAsync(db, filter, ct);
        var mode = heatMode ?? (await settings.GetAsync()).HeatMode;
        var nbpp = rows.Select(r => r.Nbpp).ToArray();
        var heat = mode == "absolute" ? nbpp.Select(v => v is null ? double.NaN : Heat.AbsoluteHeat(v.Value)).ToArray() : Heat.RelativeHeat(nbpp);
        return (rows, heat);
    }
}
