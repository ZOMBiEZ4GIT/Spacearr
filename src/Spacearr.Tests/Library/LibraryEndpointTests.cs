using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Tests.Actions;
using Spacearr.Tests.Arr;

namespace Spacearr.Tests.Library;

public class LibraryEndpointTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public LibraryEndpointTests(ArrTestApp app) => _app = app;

    [Fact]
    public async Task List_sorts_by_size_and_filters_and_pages()
    {
        var (instanceId, _) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&sort=size&order=desc&pageSize=2");
        page.GetProperty("total").GetInt32().Should().Be(6);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items[0].GetProperty("title").GetString().Should().Be("M6");
        items[0].GetProperty("heat").GetDouble().Should().BeGreaterThan(items[1].GetProperty("heat").GetDouble());
        items[0].GetProperty("color").GetString().Should().StartWith("#");

        var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&search=M3");
        filtered.GetProperty("total").GetInt32().Should().Be(1);

        // A wildly out-of-range page must not overflow the skip computation; it should
        // clamp to a real page (or come back empty) rather than 500.
        var overflow = await client.GetAsync($"/api/v1/library?instanceId={instanceId}&page=2000000000&pageSize=1000");
        overflow.StatusCode.Should().Be(HttpStatusCode.OK);
        var overflowBody = await overflow.Content.ReadFromJsonAsync<JsonElement>();
        overflowBody.GetProperty("total").GetInt32().Should().Be(6);
        overflowBody.GetProperty("items").GetArrayLength().Should().BeLessOrEqualTo(6);
    }

    [Theory]
    [InlineData("/api/v1/library")]
    [InlineData("/api/v1/library/tree")]
    [InlineData("/api/v1/library/stats")]
    [InlineData("/api/v1/duplicates")]
    public async Task Kind_filter_accepts_the_camel_case_value_the_web_app_sends(string path)
    {
        // Responses serialise MediaKind as "movie"/"episode" and the web app sends that
        // back, but minimal-API enum binding is case-sensitive - ?kind=movie was a 400.
        var (instanceId, _) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);
        foreach (var kind in new[] { "movie", "episode", "Movie" })
            (await client.GetAsync($"{path}?instanceId={instanceId}&kind={kind}")).StatusCode.Should().Be(HttpStatusCode.OK, $"kind={kind}");
        (await client.GetAsync($"{path}?instanceId={instanceId}&kind=film")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kind_filter_restricts_to_that_kind()
    {
        var (instanceId, _) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);
        var movies = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&kind=movie");
        var episodes = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&kind=episode");
        movies.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("kind").GetString() == "movie");
        (movies.GetProperty("total").GetInt32() + episodes.GetProperty("total").GetInt32()).Should().Be(6);
    }

    [Fact]
    public async Task Tree_and_stats_and_detail()
    {
        var (instanceId, itemIds) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);

        var tree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/tree?instanceId={instanceId}&colorBy=heat");
        tree.GetProperty("name").GetString().Should().Be("Library");
        tree.GetProperty("children").GetArrayLength().Should().Be(6);
        tree.GetProperty("children")[0].GetProperty("leaf").GetProperty("color").GetString().Should().Be("#D14D4D");

        var stats = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/stats?instanceId={instanceId}");
        stats.GetProperty("fileCount").GetInt32().Should().Be(6);
        stats.GetProperty("byQuality").EnumerateArray().Should().HaveCount(2);
        stats.GetProperty("largest").EnumerateArray().First().GetProperty("title").GetString().Should().Be("M6");
        stats.GetProperty("heatHistogram").GetArrayLength().Should().Be(10);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/{itemIds[5]}");
        detail.GetProperty("item").GetProperty("title").GetString().Should().Be("M6");
        var profiles = detail.GetProperty("profiles").EnumerateArray().ToList();
        profiles.Should().NotContain(p => p.GetProperty("id").GetInt32() == 5, "the current profile is excluded");
        var hd = profiles.Single(p => p.GetProperty("name").GetString() == "HD-1080p");
        hd.GetProperty("estimate").GetProperty("basis").GetString().Should().BeOneOf("library", "table", "unknown");

        // Item id 0 means "unmatched loose file" internally, not a real item - it must 404.
        var zero = await client.GetAsync("/api/v1/library/0");
        zero.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Different_search_values_get_distinct_cached_results()
    {
        // The library cache key used to be built by string-interpolating free-text
        // filter values with ":" separators, so two different (search, heatMode) pairs
        // could hash to the same key and one caller could be served another caller's
        // cached rows. Two requests differing only in `search` must get their own,
        // correct results.
        var (instanceId, _) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);

        var m1 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&search=M1");
        m1.GetProperty("total").GetInt32().Should().Be(1);
        var m1Title = m1.GetProperty("items")[0].GetProperty("title").GetString();
        m1Title.Should().Be("M1");

        var m2 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&search=M2");
        m2.GetProperty("total").GetInt32().Should().Be(1);
        var m2Title = m2.GetProperty("items")[0].GetProperty("title").GetString();
        m2Title.Should().Be("M2");

        m2Title.Should().NotBe(m1Title);

        // Under the old ":"-delimited key, search="M1" + heatMode="x:y" hashed to the
        // same string as search="M1:x" + heatMode="y" (both produce the tail
        // "M1:x:y"), so the second request would wrongly be served the first request's
        // cached (and still-fresh, within the 30s window) result. Prove the ':' is part
        // of the search text, not a field separator, by making the two requests
        // resolve to genuinely different answers against the M1..M6 seed.
        var collisionA = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/library?instanceId={instanceId}&search=M1&heatMode={Uri.EscapeDataString("x:y")}");
        collisionA.GetProperty("total").GetInt32().Should().Be(1, "search=\"M1\" matches the seeded title \"M1\"");

        var collisionB = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/library?instanceId={instanceId}&search={Uri.EscapeDataString("M1:x")}&heatMode=y");
        collisionB.GetProperty("total").GetInt32().Should().Be(0,
            "no seeded title contains ':' so search=\"M1:x\" must match nothing, even though the old key " +
            "collided this request with search=\"M1\", heatMode=\"x:y\" above");
    }

    [Fact]
    public async Task Stats_are_served_from_cache_until_a_job_finishes()
    {
        // A dedicated app so the cache state here is entirely this test's doing.
        using var app = new ArrTestApp();
        var client = await AuthedClient.CreateAsync(app);
        var instanceId = await SeedRawAsync(app, files: 1);

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/stats?instanceId={instanceId}");
        first.GetProperty("fileCount").GetInt32().Should().Be(1);

        // A second file written straight to the DB, with no job in between: the answer
        // must still come from the cache, proving the DB is not re-queried per request.
        await SeedRawAsync(app, files: 1, instanceId: instanceId);
        var cached = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/stats?instanceId={instanceId}");
        cached.GetProperty("fileCount").GetInt32().Should().Be(1, "the cached rows must be reused until a job invalidates them");

        // Any finished job invalidates it. The instance is disabled, so this enrich
        // run does no arr work at all - it just has to finish.
        var enqueued = await client.PostAsync("/api/v1/jobs/enrich", null);
        enqueued.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var jobId = (await enqueued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt32();
        await ActionEndpointTests.WaitForJob(client, jobId);

        var fresh = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/stats?instanceId={instanceId}");
        fresh.GetProperty("fileCount").GetInt32().Should().Be(2, "a finished job must invalidate the cached library rows");
    }

    /// <summary>
    /// Writes library rows directly, without the cache-version bump Seed does - this
    /// test needs data to appear behind the cache's back. The instance is disabled so
    /// the enrich job below leaves these rows alone.
    /// </summary>
    private static async Task<int> SeedRawAsync(ArrTestApp app, int files, int? instanceId = null)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var root = await db.RootFolders.FirstOrDefaultAsync();
        if (root is null)
        {
            root = new RootFolder { Path = "/mnt/cache" };
            db.RootFolders.Add(root);
            await db.SaveChangesAsync();
        }
        if (instanceId is null)
        {
            var inst = new ArrInstance { Type = ArrType.Radarr, Name = "Cached", BaseUrl = "http://radarr:7878", ApiKeyEncrypted = secrets.Protect("secret"), Enabled = false, CreatedAt = DateTime.UtcNow };
            db.ArrInstances.Add(inst);
            await db.SaveChangesAsync();
            instanceId = inst.Id;
        }
        var start = await db.MediaFiles.CountAsync();
        for (var i = 0; i < files; i++)
        {
            var n = start + i + 1;
            var file = new MediaFile { Path = $"/mnt/cache/C{n}.mkv", RootFolderId = root.Id, SizeBytes = 1_000_000 * n, ModifiedAt = DateTime.UtcNow, ScannedAt = DateTime.UtcNow,
                DurationSeconds = 3600, Width = 1920, Height = 1080, FrameRate = 24, VideoCodec = "h264", BitDepth = 8, VideoBitrateBps = 3_000_000, OverallBitrateBps = 3_200_000, Container = "mkv" };
            db.MediaFiles.Add(file);
            await db.SaveChangesAsync();
            db.MediaItems.Add(new MediaItem { ArrInstanceId = instanceId.Value, ExternalId = 500 + n, Kind = MediaKind.Movie, Title = $"C{n}", Year = 2021, MediaFileId = file.Id, ArrFileId = 600 + n, SyncedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        return instanceId.Value;
    }
}
