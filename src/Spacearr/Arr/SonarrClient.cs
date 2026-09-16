using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Spacearr.Data.Entities;

namespace Spacearr.Arr;

public sealed class SonarrClient : IArrClient
{
    private readonly ArrHttp _http;
    public SonarrClient(HttpClient http, string apiKey, ArrTimeouts? timeouts = null) => _http = new ArrHttp(http, apiKey, timeouts);
    public ArrType Type => ArrType.Sonarr;

    public async Task<ArrStatus> GetStatusAsync(CancellationToken ct)
    {
        var n = await _http.GetAsync("/api/v3/system/status", ct);
        return new ArrStatus(ArrHttp.Str(n["version"]) ?? "?", ArrHttp.Str(n["appName"]) ?? "Sonarr");
    }

    public async Task<IReadOnlyList<ArrProfile>> GetProfilesAsync(CancellationToken ct) =>
        ArrHttp.Array(await _http.GetAsync("/api/v3/qualityprofile", ct), "quality profiles").Select(p => new ArrProfile(ArrHttp.Int(p!["id"]) ?? 0, ArrHttp.Str(p["name"]) ?? "")).ToList();

    public async Task<IReadOnlyList<ArrTag>> GetTagsAsync(CancellationToken ct) =>
        ArrHttp.Array(await _http.GetAsync("/api/v3/tag", ct), "tags").Select(t => new ArrTag(ArrHttp.Int(t!["id"]) ?? 0, ArrHttp.Str(t["label"]) ?? "")).ToList();

    public async Task<IReadOnlyList<ArrRootFolder>> GetRootFoldersAsync(CancellationToken ct) =>
        ArrHttp.Array(await _http.GetAsync("/api/v3/rootfolder", ct), "root folders").Select(r => new ArrRootFolder(ArrHttp.Str(r!["path"]) ?? "")).ToList();

    public async Task<IReadOnlyList<ArrItem>> GetItemsAsync(CancellationToken ct)
    {
        var series = ArrHttp.Array(await _http.GetBulkAsync("/api/v3/series", ct), "series");
        // A large Sonarr library means two round trips per series; doing them four at a
        // time keeps a sync from being one long serial stall without hammering the app.
        var bag = new ConcurrentBag<ArrItem>();
        await Parallel.ForEachAsync(series, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (s, token) =>
        {
            var seriesId = ArrHttp.Int(s!["id"]) ?? 0;
            var files = ArrHttp.Array(await _http.GetBulkAsync($"/api/v3/episodefile?seriesId={seriesId}", token), "episode files");
            if (files.Count == 0) return;
            var episodes = ArrHttp.Array(await _http.GetBulkAsync($"/api/v3/episode?seriesId={seriesId}", token), "episodes")
                .Select(e => e!.AsObject()).Where(e => (ArrHttp.Int(e["episodeFileId"]) ?? 0) > 0)
                .GroupBy(e => ArrHttp.Int(e["episodeFileId"])!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => ArrHttp.Int(e["episodeNumber"])).ToList());
            var poster = ArrHttp.Poster(s["images"]);
            foreach (var f in files)
            {
                var fileId = ArrHttp.Int(f!["id"]) ?? 0;
                episodes.TryGetValue(fileId, out var eps);
                eps ??= new List<JsonObject>();
                bag.Add(new ArrItem(
                    ExternalId: fileId, Kind: MediaKind.Episode,
                    Title: eps.Count == 0 ? $"Season {ArrHttp.Int(f["seasonNumber"])}" : string.Join(" / ", eps.Select(e => ArrHttp.Str(e["title"]) ?? "")),
                    Year: ArrHttp.Int(s["year"]),
                    SeriesId: seriesId, SeriesTitle: ArrHttp.Str(s["title"]), SeasonNumber: ArrHttp.Int(f["seasonNumber"]),
                    EpisodeNumbers: eps.Select(e => ArrHttp.Int(e["episodeNumber"]) ?? 0).ToArray(),
                    EpisodeIds: eps.Select(e => ArrHttp.Int(e["id"]) ?? 0).ToArray(),
                    QualityProfileId: ArrHttp.Int(s["qualityProfileId"]), QualityName: ArrHttp.Str(f["quality"]?["quality"]?["name"]),
                    Monitored: eps.Count == 0 ? ArrHttp.Bool(s["monitored"]) : eps.Any(e => ArrHttp.Bool(e["monitored"])),
                    TagIds: ArrHttp.Ints(s["tags"]), PosterUrl: poster,
                    TmdbId: null, TvdbId: ArrHttp.Int(s["tvdbId"]), ImdbId: ArrHttp.Str(s["imdbId"]),
                    ArrFileId: fileId, ArrPath: ArrHttp.Str(f["path"]), ArrSizeBytes: ArrHttp.Long(f["size"])));
            }
        });
        // Parallel completion order is arbitrary; sort back to a stable order so
        // callers (and tests) see the same list every run.
        return bag
            .OrderBy(i => i.SeriesTitle, StringComparer.Ordinal)
            .ThenBy(i => i.SeasonNumber ?? int.MaxValue)
            .ThenBy(i => i.EpisodeNumbers.Length == 0 ? int.MaxValue : i.EpisodeNumbers[0])
            .ToList();
    }

    public Task DeleteFileAsync(int arrFileId, CancellationToken ct) =>
        _http.SendJsonAsync(HttpMethod.Delete, $"/api/v3/episodefile/{arrFileId}", null, ct);

    public async Task SetProfileAsync(int seriesId, int profileId, CancellationToken ct)
    {
        var s = await _http.GetAsync($"/api/v3/series/{seriesId}", ct);
        s["qualityProfileId"] = profileId;
        await _http.SendJsonAsync(HttpMethod.Put, $"/api/v3/series/{seriesId}", s, ct);
    }

    public async Task UnmonitorAsync(int episodeId, CancellationToken ct)
    {
        var e = await _http.GetAsync($"/api/v3/episode/{episodeId}", ct);
        e["monitored"] = false;
        await _http.SendJsonAsync(HttpMethod.Put, $"/api/v3/episode/{episodeId}", e, ct);
    }

    public Task SearchAsync(int[] episodeIds, CancellationToken ct) =>
        _http.SendJsonAsync(HttpMethod.Post, "/api/v3/command", new JsonObject { ["name"] = "EpisodeSearch", ["episodeIds"] = new JsonArray(episodeIds.Select(i => (JsonNode)i).ToArray()) }, ct);
}
