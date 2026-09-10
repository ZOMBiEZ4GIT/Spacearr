using FluentAssertions;
using Spacearr.Arr;
using Spacearr.Data.Entities;

namespace Spacearr.Tests.Arr;

public class SonarrClientTests
{
    private static (SonarrClient Client, FakeArrHandler Handler) Make()
    {
        var handler = new FakeArrHandler()
            .MapFixture("GET", "/api/v3/system/status", "arr-status.json")
            .MapFixture("GET", "/api/v3/qualityprofile", "sonarr-qualityprofile.json")
            .Map("GET", "/api/v3/tag", """[{"id":1,"label":"kids"}]""")
            .Map("GET", "/api/v3/rootfolder", """[{"path":"/data/tv"}]""")
            .MapFixture("GET", "/api/v3/series", "sonarr-series.json")
            .MapFixture("GET", "/api/v3/episodefile?seriesId=3", "sonarr-episodefile.json")
            .MapFixture("GET", "/api/v3/episode?seriesId=3", "sonarr-episode.json")
            .Map("GET", "/api/v3/series/3", FakeArrHandler.First("sonarr-series.json"))
            .Map("PUT", "/api/v3/series/3", "{}")
            .Map("GET", "/api/v3/episode/9001", """{"id":9001,"seriesId":3,"monitored":true}""")
            .Map("PUT", "/api/v3/episode/9001", "{}")
            .Map("DELETE", "/api/v3/episodefile/501", "{}")
            .Map("POST", "/api/v3/command", """{"id":1}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://sonarr:8989/") };
        return (new SonarrClient(http, "secret"), handler);
    }

    [Fact]
    public async Task Items_are_one_per_episode_file_with_episode_numbers_and_ids()
    {
        var (c, _) = Make();
        var items = await c.GetItemsAsync(default);
        items.Should().HaveCount(2);
        var multi = items.Single(i => i.ExternalId == 501);
        multi.Kind.Should().Be(MediaKind.Episode);
        multi.SeriesId.Should().Be(3);
        multi.SeriesTitle.Should().Be("Show");
        multi.SeasonNumber.Should().Be(1);
        multi.EpisodeNumbers.Should().Equal(1, 2);
        multi.EpisodeIds.Should().Equal(9001, 9002);
        multi.Title.Should().Be("Pilot / Second");
        multi.QualityProfileId.Should().Be(6);
        multi.QualityName.Should().Be("WEBDL-1080p");
        multi.PosterUrl.Should().Be("/MediaCover/3/poster.jpg?lastWrite=1");
        multi.TvdbId.Should().Be(9999);
        multi.ArrPath.Should().Be("/data/tv/Show/Season 01/Show - S01E01-E02.mkv");
    }

    [Fact]
    public async Task Set_profile_is_series_level_search_takes_episode_ids_unmonitor_puts_episode()
    {
        var (c, h) = Make();
        await c.SetProfileAsync(3, 7, default);
        h.Calls.Single(x => x.Method == HttpMethod.Put && x.Path == "/api/v3/series/3").Body.Should().Contain("\"qualityProfileId\":7");

        await c.SearchAsync(new[] { 9001, 9002 }, default);
        h.Calls.Last().Body.Should().Contain("EpisodeSearch").And.Contain("[9001,9002]");

        await c.UnmonitorAsync(9001, default);
        h.Calls.Last().Path.Should().Be("/api/v3/episode/9001");
        h.Calls.Last().Body.Should().Contain("\"monitored\":false");

        await c.DeleteFileAsync(501, default);
        h.Calls.Last().Path.Should().Be("/api/v3/episodefile/501");
    }
}
