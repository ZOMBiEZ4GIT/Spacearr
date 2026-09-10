using System.Text.Json.Nodes;
using Spacearr.Data.Entities;

namespace Spacearr.Arr;

public sealed class RadarrClient : IArrClient
{
    private readonly ArrHttp _http;
    public RadarrClient(HttpClient http, string apiKey) => _http = new ArrHttp(http, apiKey);
    public ArrType Type => ArrType.Radarr;

    public async Task<ArrStatus> GetStatusAsync(CancellationToken ct)
    {
        var n = await _http.GetAsync("/api/v3/system/status", ct);
        return new ArrStatus(ArrHttp.Str(n["version"]) ?? "?", ArrHttp.Str(n["appName"]) ?? "Radarr");
    }

    public async Task<IReadOnlyList<ArrProfile>> GetProfilesAsync(CancellationToken ct) =>
        (await _http.GetAsync("/api/v3/qualityprofile", ct)).AsArray().Select(p => new ArrProfile(ArrHttp.Int(p!["id"]) ?? 0, ArrHttp.Str(p["name"]) ?? "")).ToList();

    public async Task<IReadOnlyList<ArrTag>> GetTagsAsync(CancellationToken ct) =>
        (await _http.GetAsync("/api/v3/tag", ct)).AsArray().Select(t => new ArrTag(ArrHttp.Int(t!["id"]) ?? 0, ArrHttp.Str(t["label"]) ?? "")).ToList();

    public async Task<IReadOnlyList<ArrRootFolder>> GetRootFoldersAsync(CancellationToken ct) =>
        (await _http.GetAsync("/api/v3/rootfolder", ct)).AsArray().Select(r => new ArrRootFolder(ArrHttp.Str(r!["path"]) ?? "")).ToList();

    public async Task<IReadOnlyList<ArrItem>> GetItemsAsync(CancellationToken ct)
    {
        var movies = (await _http.GetAsync("/api/v3/movie", ct)).AsArray();
        var items = new List<ArrItem>();
        foreach (var m in movies)
        {
            var file = m!["movieFile"];
            if (file is null) continue;
            items.Add(new ArrItem(
                ExternalId: ArrHttp.Int(m["id"]) ?? 0, Kind: MediaKind.Movie,
                Title: ArrHttp.Str(m["title"]) ?? "", Year: ArrHttp.Int(m["year"]),
                SeriesId: null, SeriesTitle: null, SeasonNumber: null, EpisodeNumbers: Array.Empty<int>(), EpisodeIds: Array.Empty<int>(),
                QualityProfileId: ArrHttp.Int(m["qualityProfileId"]), QualityName: ArrHttp.Str(file["quality"]?["quality"]?["name"]),
                Monitored: ArrHttp.Bool(m["monitored"]), TagIds: ArrHttp.Ints(m["tags"]), PosterUrl: ArrHttp.Poster(m["images"]),
                TmdbId: ArrHttp.Int(m["tmdbId"]), TvdbId: null, ImdbId: ArrHttp.Str(m["imdbId"]),
                ArrFileId: ArrHttp.Int(file["id"]), ArrPath: ArrHttp.Str(file["path"]), ArrSizeBytes: ArrHttp.Long(file["size"])));
        }
        return items;
    }

    public Task DeleteFileAsync(int arrFileId, CancellationToken ct) =>
        _http.SendJsonAsync(HttpMethod.Delete, $"/api/v3/moviefile/{arrFileId}", null, ct);

    public async Task SetProfileAsync(int movieId, int profileId, CancellationToken ct)
    {
        var movie = await _http.GetAsync($"/api/v3/movie/{movieId}", ct);
        movie["qualityProfileId"] = profileId;
        await _http.SendJsonAsync(HttpMethod.Put, $"/api/v3/movie/{movieId}", movie, ct);
    }

    public async Task UnmonitorAsync(int movieId, CancellationToken ct)
    {
        var movie = await _http.GetAsync($"/api/v3/movie/{movieId}", ct);
        movie["monitored"] = false;
        await _http.SendJsonAsync(HttpMethod.Put, $"/api/v3/movie/{movieId}", movie, ct);
    }

    public Task SearchAsync(int[] movieIds, CancellationToken ct) =>
        _http.SendJsonAsync(HttpMethod.Post, "/api/v3/command", new JsonObject { ["name"] = "MoviesSearch", ["movieIds"] = new JsonArray(movieIds.Select(i => (JsonNode)i).ToArray()) }, ct);
}
