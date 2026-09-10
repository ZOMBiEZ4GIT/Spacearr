using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Tests.Arr;

namespace Spacearr.Tests.Actions;

// A Sonarr-flavoured counterpart to ArrTestApp, mapping the routes an action against
// a single episode file (series 3, episode file 501, episodes 9001/9002) needs -
// taken from the same fixtures SonarrClientTests uses.
public sealed class SonarrTestApp : TestApp
{
    public FakeArrHandler Arr { get; } = new FakeArrHandler()
        .MapFixture("GET", "/api/v3/system/status", "arr-status.json")
        .MapFixture("GET", "/api/v3/qualityprofile", "sonarr-qualityprofile.json")
        .Map("GET", "/api/v3/tag", "[]")
        .Map("GET", "/api/v3/rootfolder", """[{"path":"/data/tv"}]""")
        .Map("GET", "/api/v3/series/3", FakeArrHandler.First("sonarr-series.json"))
        .Map("PUT", "/api/v3/series/3", "{}")
        .Map("GET", "/api/v3/episode/9001", """{"id":9001,"seriesId":3,"monitored":true}""")
        .Map("PUT", "/api/v3/episode/9001", "{}")
        .Map("GET", "/api/v3/episode/9002", """{"id":9002,"seriesId":3,"monitored":true}""")
        .Map("PUT", "/api/v3/episode/9002", "{}")
        .Map("DELETE", "/api/v3/episodefile/501", "{}")
        .Map("POST", "/api/v3/command", """{"id":1}""");

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddHttpClient("arr").ConfigurePrimaryHttpMessageHandler(() => Arr);
    }
}

public class SonarrActionTests : IClassFixture<SonarrTestApp>
{
    private readonly SonarrTestApp _app;
    public SonarrActionTests(SonarrTestApp app) => _app = app;

    private async Task<int> SeedEpisode()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var root = new RootFolder { Path = "/mnt/tv" };
        db.RootFolders.Add(root);
        var inst = new ArrInstance { Type = ArrType.Sonarr, Name = "TV", BaseUrl = "http://sonarr:8989", ApiKeyEncrypted = secrets.Protect("secret"), CreatedAt = DateTime.UtcNow };
        db.ArrInstances.Add(inst);
        await db.SaveChangesAsync();
        var file = new MediaFile
        {
            Path = $"/mnt/tv/inst{inst.Id}/Show/Season 01/ep.mkv", RootFolderId = root.Id, SizeBytes = 4_000_000_000,
            ModifiedAt = DateTime.UtcNow, ScannedAt = DateTime.UtcNow, DurationSeconds = 2400, Width = 1920, Height = 1080,
            FrameRate = 24, VideoCodec = "h264", BitDepth = 8, VideoBitrateBps = 4_000_000, OverallBitrateBps = 4_200_000,
            Container = "mkv", AudioSummary = "AAC 2.0",
        };
        db.MediaFiles.Add(file);
        await db.SaveChangesAsync();
        var item = new MediaItem
        {
            ArrInstanceId = inst.Id, ExternalId = 501, Kind = MediaKind.Episode, Title = "Pilot / Second",
            SeriesId = 3, SeriesTitle = "Show", SeasonNumber = 1, EpisodeNumbers = "1,2", EpisodeIds = "9001,9002",
            QualityProfileId = 6, QualityProfileName = "HD-1080p", Monitored = true,
            MediaFileId = file.Id, ArrFileId = 501, ArrPath = "/data/tv/Show/Season 01/ep.mkv", TvdbId = 9999, SyncedAt = DateTime.UtcNow,
        };
        db.MediaItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task Delete_with_unmonitor_previews_one_step_per_real_episode_id()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemId = await SeedEpisode();

        var preview = await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "delete", itemId, unmonitor = true });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var steps = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("steps").EnumerateArray()
            .Select(x => $"{x.GetProperty("method").GetString()} {x.GetProperty("path").GetString()}").ToList();

        // The preview must name the episodes the job will actually PUT to, not a
        // placeholder the user cannot check against Sonarr.
        steps.Should().Contain("DELETE /api/v3/episodefile/501");
        steps.Should().Contain("PUT /api/v3/episode/9001");
        steps.Should().Contain("PUT /api/v3/episode/9002");
        steps.Should().NotContain(x => x.Contains("{id}"));
    }

    [Fact]
    public async Task Replace_on_sonarr_episode_sets_series_profile_and_searches_episode_ids()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemId = await SeedEpisode();
        _app.Arr.Calls.Clear();

        var preview = await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "replace", itemId, targetProfileId = 7 });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var p = await preview.Content.ReadFromJsonAsync<JsonElement>();
        p.GetProperty("warning").GetString().Should().Contain("Show");
        var token = p.GetProperty("confirmToken").GetString();

        var exec = await client.PostAsJsonAsync("/api/v1/actions/execute", new { type = "replace", itemId, targetProfileId = 7, confirmToken = token });
        exec.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var jobId = (await exec.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt32();

        await ActionEndpointTests.WaitForJob(client, jobId);

        var calls = _app.Arr.Calls.Where(c => c.Method != HttpMethod.Get).ToList();
        calls.Select(c => $"{c.Method} {c.Path}").Should().ContainInOrder("PUT /api/v3/series/3", "DELETE /api/v3/episodefile/501", "POST /api/v3/command");
        var command = calls.Single(c => c.Path == "/api/v3/command");
        command.Body.Should().Contain("EpisodeSearch").And.Contain("[9001,9002]");
    }
}
