using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public class FileDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spacearr-disc-" + Guid.NewGuid().ToString("N"));
    public FileDiscoveryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private void Make(string relative, int sizeBytes)
    {
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[sizeBytes]);
    }

    [Fact]
    public void Finds_media_by_extension_recursively_and_skips_small_hidden_and_other_files()
    {
        Make("Movies/Film (2020)/film.mkv", 2_000_000);
        Make("Movies/Film (2020)/film.nfo", 2_000_000);
        Make("Movies/Film (2020)/sample.mkv", 500_000);
        Make(".hidden/secret.mkv", 2_000_000);
        Make("TV/Show/Season 01/e01.MP4", 2_000_000);

        var found = new FileDiscovery(NullLogger<FileDiscovery>.Instance)
            .Enumerate(_root, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4" }, CancellationToken.None)
            .Select(f => PathNormalizer.Normalize(Path.GetRelativePath(_root, f.Path))).OrderBy(x => x).ToList();

        found.Should().Equal("Movies/Film (2020)/film.mkv", "TV/Show/Season 01/e01.MP4");
    }

    [Fact]
    public void Reports_size_and_utc_mtime()
    {
        Make("a.mkv", 3_000_000);
        var f = new FileDiscovery(NullLogger<FileDiscovery>.Instance)
            .Enumerate(_root, new HashSet<string> { ".mkv" }, CancellationToken.None).Single();
        f.SizeBytes.Should().Be(3_000_000);
        f.ModifiedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }
}
