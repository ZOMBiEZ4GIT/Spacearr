using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Spacearr.Data;
using Spacearr.Infrastructure;

namespace Spacearr.Posters;

public static class PosterEndpoints
{
    public static IEndpointRouteBuilder MapPosterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/posters/{itemId:int}", async (int itemId, SpacearrDb db, ConfigPaths paths, ISecretProtector secrets, IHttpClientFactory httpFactory, ILoggerFactory logs, HttpContext http, CancellationToken ct) =>
        {
            var log = logs.CreateLogger("Posters");
            var item = await db.MediaItems.AsNoTracking().Include(i => i.ArrInstance).SingleOrDefaultAsync(i => i.Id == itemId, ct);
            if (item?.PosterUrl is null || item.ArrInstance is null) return Results.NotFound();

            // Only ever proxy an arr-relative path (e.g. "/MediaCover/10/poster.jpg"). A
            // PosterUrl that is already absolute must never be fetched - that would let
            // Spacearr be used as an open server-side fetcher for arbitrary URLs.
            if (Uri.TryCreate(item.PosterUrl, UriKind.Absolute, out _)) return Results.NotFound();

            var cacheFile = Path.Combine(paths.PosterCacheDirectory, $"{item.ArrInstanceId}-{item.ExternalId}.jpg");
            if (!File.Exists(cacheFile))
            {
                try
                {
                    var client = httpFactory.CreateClient("arr");
                    using var req = new HttpRequestMessage(HttpMethod.Get, item.ArrInstance.BaseUrl.TrimEnd('/') + "/" + item.PosterUrl.TrimStart('/'));
                    req.Headers.Add("X-Api-Key", secrets.Unprotect(item.ArrInstance.ApiKeyEncrypted));
                    using var resp = await client.SendAsync(req, ct);
                    if (!resp.IsSuccessStatusCode) { log.LogDebug("Poster fetch for item {Id} returned {Status}", itemId, resp.StatusCode); return Results.NotFound(); }
                    var tmp = cacheFile + ".tmp";
                    await using (var fs = File.Create(tmp)) await resp.Content.CopyToAsync(fs, ct);
                    File.Move(tmp, cacheFile, overwrite: true);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    log.LogDebug(ex, "Poster fetch failed for item {Id}", itemId);
                    return Results.NotFound();
                }
            }
            var info = new FileInfo(cacheFile);
            http.Response.Headers[HeaderNames.CacheControl] = "private, max-age=86400";
            return Results.File(cacheFile, "image/jpeg", lastModified: info.LastWriteTimeUtc, entityTag: new EntityTagHeaderValue($"\"{info.Length}-{info.LastWriteTimeUtc.Ticks}\""));
        }).RequireAuthorization();
        return app;
    }
}
