using System.Text.RegularExpressions;

namespace Spacearr.Scanning;

public static partial class PathNormalizer
{
    [GeneratedRegex("/{2,}")]
    private static partial Regex MultiSlash();

    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var p = MultiSlash().Replace(path.Trim().Replace('\\', '/'), "/");
        if (p.Length > 1 && p.EndsWith('/') && !(p.Length == 3 && p[1] == ':'))
            p = p.TrimEnd('/');
        if (p.Length == 2 && p[1] == ':') p += "/";
        return p;
    }

    public static string Key(string path)
    {
        var n = Normalize(path);
        return OperatingSystem.IsWindows() ? n.ToLowerInvariant() : n;
    }

    public static bool Equal(string a, string b) => Key(a) == Key(b);

    public static bool StartsWithSegment(string path, string prefix)
    {
        var p = Key(path);
        var x = Key(prefix);
        if (!p.StartsWith(x, StringComparison.Ordinal)) return false;
        if (p.Length == x.Length) return true;
        return x.EndsWith('/') || p[x.Length] == '/';
    }
}
