using Microsoft.Extensions.Logging;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

/// <summary>
/// Wraps the real FileDiscovery so a test can simulate a root that scans
/// successfully except for one unreadable subdirectory, without depending on
/// filesystem ACL manipulation (unreliable on Windows / in CI). When
/// SkipEnabled is false this behaves exactly like the real FileDiscovery.
/// </summary>
public sealed class FlakyFileDiscovery : IFileDiscovery
{
    private readonly FileDiscovery _inner;
    public FlakyFileDiscovery(ILogger<FileDiscovery> log) => _inner = new FileDiscovery(log);

    public bool SkipEnabled { get; set; }
    public string? SkipSubdirectory { get; set; }

    public IEnumerable<DiscoveredFile> Enumerate(string root, IReadOnlySet<string> extensions, CancellationToken ct, Action<string>? onUnreadable = null)
    {
        var skip = SkipEnabled ? SkipSubdirectory : null;
        if (skip is not null) onUnreadable?.Invoke(skip);
        foreach (var f in _inner.Enumerate(root, extensions, ct))
        {
            if (skip is not null && PathNormalizer.StartsWithSegment(f.Path, skip)) continue;
            yield return f;
        }
    }
}
