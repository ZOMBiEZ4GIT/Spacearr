using FluentAssertions;
using Spacearr.Arr;
using Spacearr.Data.Entities;

namespace Spacearr.Tests.Arr;

public class PathMapperTests
{
    [Fact]
    public void Maps_longest_prefix_first()
    {
        var mapper = new PathMapper(new[]
        {
            new PathMapping { RemotePrefix = "/data", LocalPrefix = "/mnt/all" },
            new PathMapping { RemotePrefix = "/data/movies", LocalPrefix = "/mnt/movies" },
        });
        mapper.Map("/data/movies/Film (2020)/film.mkv").Should().Be("/mnt/movies/Film (2020)/film.mkv");
        mapper.Map("/data/tv/Show/S01/e01.mkv").Should().Be("/mnt/all/tv/Show/S01/e01.mkv");
    }

    [Fact]
    public void Unmapped_path_is_returned_normalised()
    {
        var mapper = new PathMapper(Array.Empty<PathMapping>());
        mapper.Map(@"D:\Movies\a.mkv").Should().Be("D:/Movies/a.mkv");
    }

    [Fact]
    public void Prefix_must_match_on_a_segment_boundary()
    {
        var mapper = new PathMapper(new[] { new PathMapping { RemotePrefix = "/data/movies", LocalPrefix = "/mnt/movies" } });
        mapper.Map("/data/movies2/x.mkv").Should().Be("/data/movies2/x.mkv");
    }

    [Fact]
    public void Windows_style_remote_prefix_maps_to_linux_local()
    {
        var mapper = new PathMapper(new[] { new PathMapping { RemotePrefix = @"M:\Movies", LocalPrefix = "/media/movies" } });
        mapper.Map(@"M:\Movies\Film\film.mkv").Should().Be("/media/movies/Film/film.mkv");
    }
}
