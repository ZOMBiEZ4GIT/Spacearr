using FluentAssertions;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public class PathNormalizerTests
{
    [Theory]
    [InlineData(@"C:\Media\Movies\", "C:/Media/Movies")]
    [InlineData("/data//movies/", "/data/movies")]
    [InlineData("/", "/")]
    [InlineData(@"D:\", "D:/")]
    [InlineData("/data/movies/Film (2020)/film.mkv", "/data/movies/Film (2020)/film.mkv")]
    [InlineData(@"\\nas\media\", "//nas/media")]
    [InlineData("//nas//media", "//nas/media")]
    public void Normalize_examples(string input, string expected) =>
        PathNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Key_is_case_insensitive_only_on_windows()
    {
        var a = PathNormalizer.Key("/Data/Movies");
        var b = PathNormalizer.Key("/data/movies");
        if (OperatingSystem.IsWindows()) a.Should().Be(b); else a.Should().NotBe(b);
    }

    [Fact]
    public void Equal_uses_key() =>
        PathNormalizer.Equal(@"C:\x\y\", "C:/x/y").Should().BeTrue();
}
