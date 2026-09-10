using System.Text.Json;
using FluentAssertions;
using Spacearr.Data.Entities;
using Spacearr.Library;

namespace Spacearr.Tests.Library;

public class TreeBuilderTests
{
    private static LibraryRow Row(int id, string title, long bytes, MediaKind kind = MediaKind.Movie, string? series = null, int? season = null, string codec = "h264") =>
        new(id, 1, "R", ArrType.Radarr, kind, title, 2020, series is null ? null : 3, series, season, null, "HD", 4, "Bluray-1080p", true, null, null, id, $"/x/{id}.mkv", bytes, 3600, 1920, 1080, 24, codec, 8, null, 8_000_000, 8_200_000, "AAC 2.0", null, 0.16, null, null);

    [Fact]
    public void Movies_are_leaves_and_episodes_nest_by_series_and_season()
    {
        var rows = new[]
        {
            Row(1, "Film", 10_000),
            Row(2, "Pilot", 3_000, MediaKind.Episode, "Show", 1),
            Row(3, "Second", 2_000, MediaKind.Episode, "Show", 1),
            Row(4, "Finale", 4_000, MediaKind.Episode, "Show", 2),
        };
        var tree = TreeBuilder.Build(rows, new[] { 0.1, 0.5, 0.9, 0.2 }, "heat", foldBelowBytes: 0);
        tree.Bytes.Should().Be(19_000);
        tree.Children!.Should().HaveCount(2);
        var film = tree.Children!.Single(c => c.Name == "Film");
        film.Leaf!.ItemId.Should().Be(1);
        var show = tree.Children!.Single(c => c.Name == "Show");
        show.Bytes.Should().Be(9_000);
        show.Children!.Select(c => c.Name).Should().Equal("Season 1", "Season 2");
        show.Children![0].Children!.Should().HaveCount(2);
    }

    [Fact]
    public void Small_leaves_fold_into_other_with_weighted_heat()
    {
        var rows = new[] { Row(1, "Big", 100_000), Row(2, "Tiny1", 100), Row(3, "Tiny2", 300) };
        var tree = TreeBuilder.Build(rows, new[] { 0.2, 1.0, 0.0 }, "heat", foldBelowBytes: 1_000);
        tree.Children!.Should().HaveCount(2);
        var other = tree.Children!.Single(c => c.Name.StartsWith("Other"));
        other.Name.Should().Be("Other (2 files)");
        other.Bytes.Should().Be(400);
        other.Leaf!.Heat.Should().BeApproximately(0.25, 0.001);
    }

    [Fact]
    public void Max_leaves_folds_smallest_first()
    {
        var rows = Enumerable.Range(1, 50).Select(i => Row(i, $"M{i}", i * 1000L)).ToArray();
        var tree = TreeBuilder.Build(rows, rows.Select(_ => 0.5).ToArray(), "heat", foldBelowBytes: 0, maxLeaves: 10);
        tree.Children!.Count(c => c.Leaf is not null && !c.Name.StartsWith("Other")).Should().Be(10);
        tree.Children!.Single(c => c.Name.StartsWith("Other")).Name.Should().Be("Other (40 files)");
        tree.Children!.Where(c => !c.Name.StartsWith("Other")).Min(c => c.Bytes).Should().Be(41_000);
    }

    [Fact]
    public void Unreadable_files_serialise_as_negative_heat_instead_of_nan()
    {
        // NaN heat (a file ffprobe couldn't read) used to reach the leaf verbatim, and
        // System.Text.Json refuses to write NaN - one unreadable file 500'd /library/tree.
        var rows = new[] { Row(1, "Readable", 100_000), Row(2, "Unreadable", 50_000), Row(3, "Tiny unreadable", 100) };
        var tree = TreeBuilder.Build(rows, new[] { 0.4, double.NaN, double.NaN }, "heat", foldBelowBytes: 1_000);

        var act = () => JsonSerializer.Serialize(tree, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        act.Should().NotThrow();
        tree.Children!.Single(c => c.Name == "Unreadable").Leaf!.Heat.Should().Be(-1);
        tree.Children!.Single(c => c.Name.StartsWith("Other")).Leaf!.Heat.Should().Be(-1);
        tree.Children!.Single(c => c.Name == "Unreadable").Leaf!.Color.Should().Be(Heat.UnknownColor);
        tree.Children!.Single(c => c.Name == "Readable").Leaf!.Heat.Should().Be(0.4);
    }

    [Fact]
    public void Categorical_colour_is_stable_per_value()
    {
        var rows = new[] { Row(1, "A", 10, codec: "hevc"), Row(2, "B", 10, codec: "hevc"), Row(3, "C", 10, codec: "h264") };
        var tree = TreeBuilder.Build(rows, new[] { 0.0, 0.0, 0.0 }, "codec", 0);
        var colours = tree.Children!.ToDictionary(c => c.Name, c => c.Leaf!.Color);
        colours["A"].Should().Be(colours["B"]);
        colours["A"].Should().NotBe(colours["C"]);
    }
}
