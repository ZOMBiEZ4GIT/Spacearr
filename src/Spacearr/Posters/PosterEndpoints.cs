using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Spacearr.Data;
using Spacearr.Infrastructure;

namespace Spacearr.Posters;

public static class PosterEndpoints
{
    // Posters are small cover art, never legitimately this big - caps how much a
    // huge or corrupt upstream response can write to disk.
    private const long MaxPosterBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapPosterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/posters/{itemId:int}", async (int itemId, SpacearrDb db, ConfigPaths paths, ISecretProtector secrets, IHttpClientFactory httpFactory, ILoggerFactory logs, HttpContext http, CancellationToken ct) =>
        {
            var log = logs.CreateLogger("Posters");
            var item = await db.MediaItems.AsNoTracking().Include(i => i.ArrInstance).SingleOrDefaultAsync(i => i.Id == itemId, ct);
            if (item?.PosterUrl is null || item.ArrInstance is null) return Results.NotFound();

            // Only ever proxy an arr-relative /MediaCover/... path. This rejects an
            // absolute URL (which would let Spacearr be used as an open server-side
            // fetcher for arbitrary URLs) and any ".." or backslash segment (which could
            // otherwise escape /MediaCover and reach other same-host arr endpoints - e.g.
            // /api/v3/config/host - using the real, server-held API key). Unescaping first
            // catches a percent-encoded "%2e%2e" traversal attempt too.
            var decodedPosterUrl = Uri.UnescapeDataString(item.PosterUrl);
            if (decodedPosterUrl.Contains("..", StringComparison.Ordinal) ||
                decodedPosterUrl.Contains('\\') ||
                !decodedPosterUrl.StartsWith("/MediaCover/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            // Radarr/Sonarr cover URLs carry a `?lastWrite=...` query precisely so replaced
            // artwork gets a new URL - folding a short hash of the full PosterUrl into the cache
            // file name means a changed lastWrite produces a new file instead of being masked by
            // the old one under the (instance, item) key alone. (Eviction of files for removed
            // items/instances is a separate, deliberately unaddressed concern.)
            var urlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.PosterUrl)))[..12].ToLowerInvariant();
            var cacheFile = Path.Combine(paths.PosterCacheDirectory, $"{item.ArrInstanceId}-{item.ExternalId}-{urlHash}.jpg");
            if (!File.Exists(cacheFile))
            {
                // Unique per request so two concurrent misses for the same item never
                // race on, or delete, each other's temp file.
                var tmp = cacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var client = httpFactory.CreateClient("arr");
                    using var req = new HttpRequestMessage(HttpMethod.Get, item.ArrInstance.BaseUrl.TrimEnd('/') + "/" + item.PosterUrl.TrimStart('/'));
                    req.Headers.Add("X-Api-Key", secrets.Unprotect(item.ArrInstance.ApiKeyEncrypted));
                    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!resp.IsSuccessStatusCode) { log.LogDebug("Poster fetch for item {Id} returned {Status}", itemId, resp.StatusCode); return Results.NotFound(); }
                    if (resp.Content.Headers.ContentLength is { } declaredLength && declaredLength > MaxPosterBytes)
                    {
                        log.LogDebug("Poster fetch for item {Id} declared {Length} bytes, exceeding the {Max} byte cap", itemId, declaredLength, MaxPosterBytes);
                        return Results.NotFound();
                    }

                    // Copy through a bounded loop rather than CopyToAsync: a lying or
                    // chunked-without-Content-Length upstream response must not be able to
                    // stream an unbounded amount of data to disk.
                    var written = 0L;
                    var buffer = new byte[81920];
                    await using (var source = await resp.Content.ReadAsStreamAsync(ct))
                    await using (var fs = File.Create(tmp))
                    {
                        int read;
                        while ((read = await source.ReadAsync(buffer, ct)) > 0)
                        {
                            written += read;
                            if (written > MaxPosterBytes)
                            {
                                log.LogDebug("Poster fetch for item {Id} exceeded the {Max} byte cap while streaming; aborting", itemId, MaxPosterBytes);
                                return Results.NotFound();
                            }
                            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                        }
                    }
                    File.Move(tmp, cacheFile, overwrite: true);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    log.LogDebug(ex, "Poster fetch failed for item {Id}", itemId);
                    return Results.NotFound();
                }
                finally
                {
                    // Only ever removes a leftover/aborted temp file - a completed fetch
                    // has already moved it to cacheFile by this point, so it no longer exists.
                    // A locked tmp file (AV scan, read-only volume) must not turn an endpoint
                    // documented to return 404 on any failure into an unhandled 500.
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort cleanup only */ }
                }
            }
            var info = new FileInfo(cacheFile);
            http.Response.Headers[HeaderNames.CacheControl] = "private, max-age=86400";
            return Results.File(cacheFile, "image/jpeg", lastModified: info.LastWriteTimeUtc, entityTag: new EntityTagHeaderValue($"\"{info.Length}-{info.LastWriteTimeUtc.Ticks}\""));
        }).RequireAuthorization();
        return app;
    }
}
