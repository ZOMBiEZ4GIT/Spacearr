using System.Text.RegularExpressions;
using Spacearr.Data.Entities;

namespace Spacearr.Library;

public sealed record DuplicateGroup(string Key, string Title, LibraryRow[] Members, long WastedBytes);

public static partial class DuplicateFinder
{
    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlnum();

    public static List<DuplicateGroup> Find(IReadOnlyList<LibraryRow> rows)
    {
        var groups = new Dictionary<string, List<LibraryRow>>();
        foreach (var r in rows)
        {
            if (r.ItemId == 0) continue;
            var key = KeyFor(r);
            if (key is null) continue;
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<LibraryRow>();
            list.Add(r);
        }
        var result = new List<DuplicateGroup>();
        foreach (var (key, members) in groups)
        {
            var distinct = members.GroupBy(m => m.FileId).Select(g => g.First()).OrderByDescending(m => m.SizeBytes).ToArray();
            if (distinct.Length < 2) continue;
            var total = distinct.Sum(m => m.SizeBytes);
            result.Add(new DuplicateGroup(key, TitleFor(distinct[0]), distinct, total - distinct[0].SizeBytes));
        }
        return result.OrderByDescending(g => g.WastedBytes).ToList();
    }

    private static string? KeyFor(LibraryRow r)
    {
        if (r.Kind == MediaKind.Movie)
            return r.TmdbId is > 0 ? $"tmdb:{r.TmdbId}" : $"title:{Norm(r.Title)}:{r.Year}";
        if (string.IsNullOrEmpty(r.Episodes)) return null;
        return r.TvdbId is > 0 ? $"tvdb:{r.TvdbId}:s{r.SeasonNumber}:e{r.Episodes}" : $"series:{Norm(r.SeriesTitle ?? "")}:s{r.SeasonNumber}:e{r.Episodes}";
    }

    private static string Norm(string s) => NonAlnum().Replace(s.ToLowerInvariant(), "");

    public static string TitleFor(LibraryRow r) => r.Kind == MediaKind.Movie
        ? (r.Year is null ? r.Title : $"{r.Title} ({r.Year})")
        : string.IsNullOrEmpty(r.Episodes)
            ? $"{r.SeriesTitle} S{r.SeasonNumber:00}"
            : $"{r.SeriesTitle} S{r.SeasonNumber:00}E{string.Join("-E", r.Episodes.Split(',').Select(e => int.TryParse(e, out var n) ? n.ToString("00") : e))}";
}
