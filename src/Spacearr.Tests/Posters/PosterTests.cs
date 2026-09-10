using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Infrastructure;
using Spacearr.Tests.Arr;
using Spacearr.Tests.Library;

namespace Spacearr.Tests.Posters;

public class PosterTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public PosterTests(ArrTestApp app) => _app = app;

    [Fact]
    public async Task Fetches_from_arr_once_then_serves_from_cache()
    {
        var (instanceId, itemIds) = await Seed.LibraryAsync(_app, movies: 1);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var item = await db.MediaItems.SingleAsync(i => i.Id == itemIds[0]);
            item.PosterUrl = "/MediaCover/10/poster.jpg?lastWrite=1";
            await db.SaveChangesAsync();
        }
        _app.Arr.Map("GET", "/MediaCover/10/poster.jpg?lastWrite=1", "JPEGDATA");
        var client = await AuthedClient.CreateAsync(_app);

        var first = await client.GetAsync($"/api/v1/posters/{itemIds[0]}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadAsStringAsync()).Should().Be("JPEGDATA");
        first.Headers.CacheControl!.Private.Should().BeTrue();

        var before = _app.Arr.Calls.Count(c => c.Path.StartsWith("/MediaCover"));
        var second = await client.GetAsync($"/api/v1/posters/{itemIds[0]}");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        _app.Arr.Calls.Count(c => c.Path.StartsWith("/MediaCover")).Should().Be(before);

        (await client.GetAsync("/api/v1/posters/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        _ = instanceId;
    }

    [Fact]
    public async Task Absolute_poster_url_is_rejected_instead_of_fetched()
    {
        // A PosterUrl must be an arr-relative path. If it were ever absolute (e.g. a
        // full http(s) URL), proxying it would let Spacearr be used as an open
        // server-side fetcher for arbitrary URLs - so it must 404, not be fetched.
        var (_, itemIds) = await Seed.LibraryAsync(_app, movies: 1);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var item = await db.MediaItems.SingleAsync(i => i.Id == itemIds[0]);
            item.PosterUrl = "http://evil.example.com/steal-me.jpg";
            await db.SaveChangesAsync();
        }
        var client = await AuthedClient.CreateAsync(_app);

        var before = _app.Arr.Calls.Count;
        var response = await client.GetAsync($"/api/v1/posters/{itemIds[0]}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // No outbound call should have been made at all - the guard must reject the
        // absolute URL before ever reaching the HttpClient.
        _app.Arr.Calls.Count.Should().Be(before);
    }

    [Fact]
    public async Task Path_traversal_in_poster_url_is_rejected_with_no_outbound_call()
    {
        // A PosterUrl containing ".." could otherwise escape /MediaCover and reach other
        // same-host arr endpoints (e.g. /api/v3/config/host) using the real, server-held
        // API key. Must 404 without ever calling out.
        var (_, itemIds) = await Seed.LibraryAsync(_app, movies: 1);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var item = await db.MediaItems.SingleAsync(i => i.Id == itemIds[0]);
            item.PosterUrl = "/MediaCover/10/../../api/v3/config/host";
            await db.SaveChangesAsync();
        }
        var client = await AuthedClient.CreateAsync(_app);

        var before = _app.Arr.Calls.Count;
        var response = await client.GetAsync($"/api/v1/posters/{itemIds[0]}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _app.Arr.Calls.Count.Should().Be(before);
    }

    [Fact]
    public async Task Non_MediaCover_poster_url_is_rejected_with_no_outbound_call()
    {
        // Only /MediaCover/... paths may ever be proxied - a PosterUrl pointing anywhere
        // else on the arr host (even without "..") must 404 without ever calling out.
        var (_, itemIds) = await Seed.LibraryAsync(_app, movies: 1);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var item = await db.MediaItems.SingleAsync(i => i.Id == itemIds[0]);
            item.PosterUrl = "/api/v3/config/host";
            await db.SaveChangesAsync();
        }
        var client = await AuthedClient.CreateAsync(_app);

        var before = _app.Arr.Calls.Count;
        var response = await client.GetAsync($"/api/v1/posters/{itemIds[0]}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _app.Arr.Calls.Count.Should().Be(before);
    }

    [Fact]
    public async Task Oversized_poster_response_is_rejected_and_leaves_no_cache_file()
    {
        // The arr response body must never be streamed to disk unbounded - a huge or
        // corrupt upstream response must not be able to fill the disk. Uses a dedicated
        // app (rather than the shared fixture) so this test's 11 MB fixture body and its
        // cache directory can't affect any other test.
        using var app = new ArrTestApp();
        var (_, itemIds) = await Seed.LibraryAsync(app, movies: 1);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var item = await db.MediaItems.SingleAsync(i => i.Id == itemIds[0]);
            item.PosterUrl = "/MediaCover/10/poster.jpg";
            await db.SaveChangesAsync();
        }
        app.Arr.Map("GET", "/MediaCover/10/poster.jpg", new string('x', 11 * 1024 * 1024));
        var client = await AuthedClient.CreateAsync(app);

        var response = await client.GetAsync($"/api/v1/posters/{itemIds[0]}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var paths = app.Services.GetRequiredService<ConfigPaths>();
        Directory.GetFiles(paths.PosterCacheDirectory).Should().BeEmpty(
            "no cache file, partial or complete, may be left behind for a response over the size cap");
    }
}
