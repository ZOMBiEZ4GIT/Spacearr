using Spacearr.Data.Entities;
using Spacearr.Scanning;

namespace Spacearr.Arr;

public sealed class PathMapper
{
    private readonly (string Remote, string Local)[] _rules;

    public PathMapper(IEnumerable<PathMapping> mappings)
    {
        _rules = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.RemotePrefix))
            .Select(m => (PathNormalizer.Normalize(m.RemotePrefix), PathNormalizer.Normalize(m.LocalPrefix)))
            .OrderByDescending(r => r.Item1.Length)
            .ToArray();
    }

    public string Map(string arrPath)
    {
        var normalized = PathNormalizer.Normalize(arrPath);
        foreach (var (remote, local) in _rules)
        {
            if (!PathNormalizer.StartsWithSegment(normalized, remote)) continue;
            var remainder = normalized.Substring(remote.Length).TrimStart('/');
            return PathNormalizer.Normalize(remainder.Length == 0 ? local : $"{local}/{remainder}");
        }
        return normalized;
    }
}
