namespace Spacearr.Scanning;

public sealed record DiscoveredFile(string Path, long SizeBytes, DateTime ModifiedAtUtc);

public interface IFileDiscovery
{
    IEnumerable<DiscoveredFile> Enumerate(string root, IReadOnlySet<string> extensions, CancellationToken ct, Action<string>? onUnreadable = null);
}

public sealed class FileDiscovery : IFileDiscovery
{
    public const long MinSizeBytes = 1_000_000;
    private readonly ILogger<FileDiscovery> _log;
    public FileDiscovery(ILogger<FileDiscovery> log) => _log = log;

    public IEnumerable<DiscoveredFile> Enumerate(string root, IReadOnlySet<string> extensions, CancellationToken ct, Action<string>? onUnreadable = null)
    {
        // Match case-insensitively regardless of the comparer the caller's set was
        // built with, so the contract holds even for a case-sensitive HashSet.
        var extensionSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();
            List<string> subdirs;
            List<string> files;
            try
            {
                // Materialise both enumerations inside the try: EnumerateDirectories
                // and EnumerateFiles can throw lazily while iterating (not just when
                // called), so an unreadable directory must be caught here rather than
                // aborting the whole scan.
                subdirs = Directory.EnumerateDirectories(dir).ToList();
                files = Directory.EnumerateFiles(dir).ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                _log.LogWarning("Skipping unreadable directory {Dir}: {Message}", dir, ex.Message);
                onUnreadable?.Invoke(dir);
                continue;
            }
            foreach (var sub in subdirs)
            {
                if (Path.GetFileName(sub).StartsWith('.')) continue;
                try
                {
                    // Symlinks and junctions carry FileAttributes.ReparsePoint. Following
                    // them can recurse forever (a link back to an ancestor directory), so
                    // they are skipped rather than pushed onto the walk.
                    if ((new DirectoryInfo(sub).Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        _log.LogDebug("Skipping reparse point {Dir}", sub);
                        continue;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    _log.LogDebug("Skipping directory with unreadable attributes {Dir}: {Message}", sub, ex.Message);
                    onUnreadable?.Invoke(sub);
                    continue;
                }
                pending.Push(sub);
            }
            foreach (var file in files)
            {
                if (!extensionSet.Contains(Path.GetExtension(file))) continue;
                FileInfo info;
                try { info = new FileInfo(file); if (info.Length < MinSizeBytes) continue; }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { continue; }
                yield return new DiscoveredFile(PathNormalizer.Normalize(file), info.Length, info.LastWriteTimeUtc);
            }
        }
    }
}
