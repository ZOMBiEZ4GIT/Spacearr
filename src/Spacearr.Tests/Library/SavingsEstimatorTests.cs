using FluentAssertions;
using Spacearr.Data.Entities;
using Spacearr.Library;

namespace Spacearr.Tests.Library;

public class SavingsEstimatorTests
{
    private static LibraryRow Row(int id, long bytes, double duration, string quality) =>
        new(id, 1, "R", ArrType.Radarr, MediaKind.Movie, $"M{id}", 2020, null, null, null, null, "HD", 4, quality, true, null, null, id, $"/x/{id}.mkv", bytes, duration, 1920, 1080, 24, "h264", 8, null, null, null, null, null, null, null, null);

    [Fact]
    public void Uses_library_median_when_enough_samples()
    {
        var target = Row(1, 60_000_000_000, 7200, "Bluray-2160p");
        var library = Enumerable.Range(2, 6).Select(i => Row(i, 3_000_000L * 7200 * (i % 2 == 0 ? 1 : 2), 7200, "Bluray-1080p")).ToList();
        var e = SavingsEstimator.Estimate(target, "Bluray-1080p", library);
        e.Samples.Should().Be(6);
        e.Basis.Should().Be("library");
        e.EstimatedBytes.Should().BeInRange(3_000_000L * 7200, 6_000_000L * 7200);
        e.SavingsBytes.Should().Be(target.SizeBytes - e.EstimatedBytes);
    }

    [Fact]
    public void Falls_back_to_table_and_marks_unknown_quality()
    {
        var target = Row(1, 60_000_000_000, 7200, "Bluray-2160p");
        var table = SavingsEstimator.Estimate(target, "WEBDL-1080p", new List<LibraryRow>());
        table.Basis.Should().Be("table");
        table.EstimatedBytes.Should().Be(1_200_000L * 7200);

        var unknown = SavingsEstimator.Estimate(target, "Any", new List<LibraryRow>());
        unknown.Basis.Should().Be("unknown");
        unknown.SavingsBytes.Should().Be(0);
    }
}
