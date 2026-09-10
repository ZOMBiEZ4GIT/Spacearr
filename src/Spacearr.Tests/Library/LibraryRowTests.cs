using FluentAssertions;
using Spacearr.Data.Entities;
using Spacearr.Library;

namespace Spacearr.Tests.Library;

public class LibraryRowTests
{
    private static LibraryRow Row(int? width, int? height) =>
        new(1, 1, "R", ArrType.Radarr, MediaKind.Movie, "M", 2020, null, null, null, null, "HD", 4, "Bluray-1080p", true, null, null, 1, "/x/1.mkv", 1, 3600, width, height, 24, "h264", 8, null, 8_000_000, 8_200_000, "AAC 2.0", null, 0.16);

    [Theory]
    [InlineData(1920, 804, "1080p")]   // 2.39:1 cinema master: raw height alone would misbucket as 720p
    [InlineData(3840, 1600, "2160p")]  // 2.4:1 at 4K width: raw height alone would misbucket as 1080p
    [InlineData(2560, 1440, "1440p")]  // raw height alone would misbucket as 1080p
    [InlineData(1280, 720, "720p")]
    [InlineData(null, 1080, "1080p")]  // width unknown: use height alone
    public void Resolution_buckets_on_effective_height(int? width, int? height, string expected) =>
        Row(width, height).Resolution.Should().Be(expected);

    [Fact]
    public void Resolution_is_null_when_both_dimensions_unknown() =>
        Row(null, null).Resolution.Should().BeNull();
}
