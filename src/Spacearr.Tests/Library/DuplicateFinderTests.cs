using FluentAssertions;
using Spacearr.Data.Entities;
using Spacearr.Library;

namespace Spacearr.Tests.Library;

public class DuplicateFinderTests
{
    private static LibraryRow Movie(int id, int instance, string title, int year, long bytes, int? tmdb) =>
        new(id, instance, $"I{instance}", ArrType.Radarr, MediaKind.Movie, title, year, null, null, null, null, null, null, "Bluray-1080p", true, null, null, id, $"/x/{id}.mkv", bytes, 7200, 1920, 1080, 24, "h264", 8, null, null, null, null, null, null, null, null);

    private static LibraryRow Episode(int id, int instance, string series, int season, string eps, long bytes, int? tvdb) =>
        new(id, instance, $"I{instance}", ArrType.Sonarr, MediaKind.Episode, "Ep", 2015, 3, series, season, eps, null, null, "WEBDL-1080p", true, null, null, id, $"/t/{id}.mkv", bytes, 2400, 1920, 1080, 24, "h264", 8, null, null, null, null, null, null, null, null);

    [Fact]
    public void Movies_group_by_tmdb_across_instances_and_wasted_is_total_minus_largest()
    {
        var rows = new[]
        {
            Movie(1, 1, "Film", 2020, 60_000, 12345) with { TmdbId = 12345 },
            Movie(2, 2, "Film", 2020, 20_000, 12345) with { TmdbId = 12345 },
            Movie(3, 1, "Other", 2021, 10_000, 999) with { TmdbId = 999 },
        };
        var groups = DuplicateFinder.Find(rows);
        var g = groups.Should().ContainSingle().Subject;
        g.Key.Should().Be("tmdb:12345");
        g.Members.Select(m => m.FileId).Should().BeEquivalentTo(new[] { 1, 2 });
        g.WastedBytes.Should().Be(20_000);
        g.Title.Should().Be("Film (2020)");
    }

    [Fact]
    public void Movies_without_tmdb_group_by_normalised_title_and_year()
    {
        var rows = new[] { Movie(1, 1, "The Film!", 2020, 5, null), Movie(2, 2, "the film", 2020, 6, null), Movie(3, 1, "The Film", 2021, 7, null) };
        var groups = DuplicateFinder.Find(rows);
        groups.Should().ContainSingle().Which.Key.Should().Be("title:thefilm:2020");
    }

    [Fact]
    public void Episodes_group_by_tvdb_season_and_episode_numbers()
    {
        var rows = new[]
        {
            Episode(1, 1, "Show", 1, "1", 3_000, 9999) with { TvdbId = 9999 },
            Episode(2, 2, "Show", 1, "1", 2_000, 9999) with { TvdbId = 9999 },
            Episode(3, 1, "Show", 1, "2", 2_000, 9999) with { TvdbId = 9999 },
        };
        var g = DuplicateFinder.Find(rows).Should().ContainSingle().Subject;
        g.Key.Should().Be("tvdb:9999:s1:e1");
        g.Title.Should().Be("Show S01E01");
    }

    [Fact]
    public void Unmatched_files_never_group_and_groups_sort_by_waste()
    {
        var rows = new[]
        {
            Movie(1, 1, "A", 2020, 100, 1) with { TmdbId = 1 }, Movie(2, 2, "A", 2020, 50, 1) with { TmdbId = 1 },
            Movie(3, 1, "B", 2020, 100, 2) with { TmdbId = 2 }, Movie(4, 2, "B", 2020, 90, 2) with { TmdbId = 2 },
            Movie(5, 0, "loose.mkv", 0, 100, null) with { ItemId = 0 }, Movie(6, 0, "loose.mkv", 0, 100, null) with { ItemId = 0 },
        };
        var groups = DuplicateFinder.Find(rows);
        groups.Should().HaveCount(2);
        groups[0].Key.Should().Be("tmdb:2");
    }
}
