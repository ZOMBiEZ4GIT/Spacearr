using Spacearr.Data.Entities;

namespace Spacearr.Library;

public sealed record TreeLeaf(int ItemId, int FileId, double Heat, string Color, string? PosterUrl, string? Quality, string? Codec, string? Resolution, int InstanceId, string InstanceName);
public sealed record TreeNode(string Name, long Bytes, TreeNode[]? Children, TreeLeaf? Leaf);

public static class TreeBuilder
{
    public static TreeNode Build(IReadOnlyList<LibraryRow> rows, double[] heat, string colorBy, long foldBelowBytes, int maxLeaves = 10000, ISet<int>? duplicateFileIds = null)
    {
        var leaves = new List<(LibraryRow Row, double Heat)>(rows.Count);
        for (var i = 0; i < rows.Count; i++) leaves.Add((rows[i], heat[i]));

        // Global cap: fold the smallest beyond maxLeaves into per-group "Other"
        var keep = new HashSet<int>();
        foreach (var (row, _) in leaves.OrderByDescending(l => l.Row.SizeBytes).Take(maxLeaves)) keep.Add(row.FileId);

        var top = new List<TreeNode>();
        foreach (var seriesGroup in leaves.Where(l => l.Row.Kind == MediaKind.Episode).GroupBy(l => l.Row.SeriesTitle ?? "Unknown series"))
        {
            var seasons = new List<TreeNode>();
            foreach (var seasonGroup in seriesGroup.GroupBy(l => l.Row.SeasonNumber ?? 0).OrderBy(g => g.Key))
            {
                var (kept, folded) = Split(seasonGroup, keep, foldBelowBytes);
                var children = kept.Select(l => Leaf(l.Row.Title, l.Row, l.Heat, colorBy, duplicateFileIds)).ToList();
                if (folded.Count > 0) children.Add(Other(folded, colorBy));
                seasons.Add(new TreeNode($"Season {seasonGroup.Key}", children.Sum(c => c.Bytes), children.OrderByDescending(c => c.Bytes).ToArray(), null));
            }
            top.Add(new TreeNode(seriesGroup.Key, seasons.Sum(s => s.Bytes), seasons.ToArray(), null));
        }
        var (movies, moviesFolded) = Split(leaves.Where(l => l.Row.Kind == MediaKind.Movie), keep, foldBelowBytes);
        top.AddRange(movies.Select(l => Leaf(l.Row.Title, l.Row, l.Heat, colorBy, duplicateFileIds)));
        if (moviesFolded.Count > 0) top.Add(Other(moviesFolded, colorBy));

        return new TreeNode("Library", top.Sum(t => t.Bytes), top.OrderByDescending(t => t.Bytes).ToArray(), null);
    }

    private static (List<(LibraryRow Row, double Heat)> Kept, List<(LibraryRow Row, double Heat)> Folded) Split(IEnumerable<(LibraryRow Row, double Heat)> group, HashSet<int> keep, long foldBelow)
    {
        var kept = new List<(LibraryRow, double)>(); var folded = new List<(LibraryRow, double)>();
        foreach (var l in group) (l.Row.SizeBytes >= foldBelow && keep.Contains(l.Row.FileId) ? kept : folded).Add(l);
        return (kept, folded);
    }

    private static TreeNode Leaf(string name, LibraryRow r, double heat, string colorBy, ISet<int>? dups) =>
        new(name, r.SizeBytes, null, new TreeLeaf(r.ItemId, r.FileId, heat, ColorFor(r, heat, colorBy, dups), r.PosterUrl, r.QualityName, r.VideoCodec, r.Resolution, r.InstanceId, r.InstanceName));

    private static TreeNode Other(List<(LibraryRow Row, double Heat)> folded, string colorBy)
    {
        var bytes = folded.Sum(f => f.Row.SizeBytes);
        var known = folded.Where(f => !double.IsNaN(f.Heat)).ToList();
        var heat = known.Count == 0 || bytes == 0 ? double.NaN : known.Sum(f => f.Heat * f.Row.SizeBytes) / known.Sum(f => f.Row.SizeBytes);
        return new TreeNode($"Other ({folded.Count} files)", bytes, null, new TreeLeaf(0, 0, heat, colorBy == "heat" ? Heat.Color(heat) : Heat.UnknownColor, null, null, null, null, 0, ""));
    }

    public static string ColorFor(LibraryRow r, double heat, string colorBy, ISet<int>? dups) => colorBy switch
    {
        "quality" => Heat.CategoryColor(r.QualityName),
        "codec" => Heat.CategoryColor(r.VideoCodec),
        "resolution" => Heat.CategoryColor(r.Resolution),
        "instance" => Heat.CategoryColor(r.InstanceName),
        "duplicates" => dups is not null && dups.Contains(r.FileId) ? "#D14D4D" : "#4B5563",
        _ => Heat.Color(heat),
    };
}
