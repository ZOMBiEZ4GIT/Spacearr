namespace Spacearr.Library;

public sealed record DuplicateGroup(string Key, string Title, LibraryRow[] Members, long WastedBytes);

public static class DuplicateFinder
{
    public static List<DuplicateGroup> Find(IReadOnlyList<LibraryRow> rows) => new();
}
