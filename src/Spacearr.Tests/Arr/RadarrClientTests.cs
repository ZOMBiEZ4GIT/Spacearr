using System.Net;
using FluentAssertions;
using Spacearr.Arr;
using Spacearr.Data.Entities;

namespace Spacearr.Tests.Arr;

public class RadarrClientTests
{
    private static (RadarrClient Client, FakeArrHandler Handler) Make()
    {
        var handler = new FakeArrHandler()
            .MapFixture("GET", "/api/v3/system/status", "arr-status.json")
            .MapFixture("GET", "/api/v3/qualityprofile", "radarr-qualityprofile.json")
            .MapFixture("GET", "/api/v3/tag", "radarr-tag.json")
            .Map("GET", "/api/v3/rootfolder", """[{"path":"/data/movies"}]""")
            .MapFixture("GET", "/api/v3/movie", "radarr-movie.json")
            .Map("GET", "/api/v3/movie/10", FakeArrHandler.First("radarr-movie.json"))
            .Map("PUT", "/api/v3/movie/10", "{}")
            .Map("DELETE", "/api/v3/moviefile/77", "{}")
            .Map("POST", "/api/v3/command", """{"id":1}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://radarr:7878/") };
        return (new RadarrClient(http, "secret"), handler);
    }

    [Fact]
    public async Task Status_profiles_tags_roots()
    {
        var (c, _) = Make();
        (await c.GetStatusAsync(default)).Version.Should().StartWith("6.");
        (await c.GetProfilesAsync(default)).Should().Contain(p => p.Id == 5 && p.Name == "Ultra-HD");
        (await c.GetTagsAsync(default)).Should().Contain(t => t.Label == "4k");
        (await c.GetRootFoldersAsync(default)).Single().Path.Should().Be("/data/movies");
    }

    [Fact]
    public async Task Items_only_include_movies_with_files_and_carry_poster_profile_and_file()
    {
        var (c, _) = Make();
        var items = await c.GetItemsAsync(default);
        var film = items.Should().ContainSingle().Subject;
        film.Kind.Should().Be(MediaKind.Movie);
        film.ExternalId.Should().Be(10);
        film.ArrFileId.Should().Be(77);
        film.ArrPath.Should().Be("/data/movies/Film (2020)/film.mkv");
        film.QualityProfileId.Should().Be(5);
        film.QualityName.Should().Be("Bluray-2160p");
        film.PosterUrl.Should().Be("/MediaCover/10/poster.jpg?lastWrite=1", "the local arr cover path is stored so posters are proxied, never fetched from TMDB by the browser");
        film.TmdbId.Should().Be(12345);
        film.TagIds.Should().Equal(2);
    }

    [Fact]
    public async Task Set_profile_puts_full_movie_with_new_profile_and_search_posts_command()
    {
        var (c, h) = Make();
        await c.SetProfileAsync(10, 4, default);
        var put = h.Calls.Single(x => x.Method == HttpMethod.Put);
        put.Body.Should().Contain("\"qualityProfileId\":4").And.Contain("\"title\":\"Film\"");

        await c.SearchAsync(new[] { 10 }, default);
        h.Calls.Last().Body.Should().Contain("MoviesSearch").And.Contain("[10]");

        await c.DeleteFileAsync(77, default);
        h.Calls.Last().Path.Should().Be("/api/v3/moviefile/77");
    }

    [Fact]
    public async Task Wrong_key_is_an_arr_exception_with_guidance()
    {
        var handler = new FakeArrHandler().MapFixture("GET", "/api/v3/system/status", "arr-status.json");
        var c = new RadarrClient(new HttpClient(handler) { BaseAddress = new Uri("http://radarr:7878/") }, "wrong");
        var act = () => c.GetStatusAsync(default);
        (await act.Should().ThrowAsync<ArrException>()).Which.Message.Should().Contain("API key");
    }
}
