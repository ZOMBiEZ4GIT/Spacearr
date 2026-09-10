namespace Spacearr.Scanning;

public sealed record DiscoveredFile(string Path, long SizeBytes, DateTime ModifiedAtUtc);

public interface IFileDiscovery
{
    IEnumerable<DiscoveredFile> Enumerate(string root, IReadOnlySet<string> extensions, CancellationToken ct);
}

public sealed class FileDiscovery : IFileDiscovery
{
    public const long MinSizeBytes = 1_000_000;
    private readonly ILogger<FileDiscovery> _log;
    public FileDiscovery(ILogger<FileDiscovery> log) => _log = log;

    public IEnumerable<DiscoveredFile> Enumerate(string root, IReadOnlySet<string> extensions, CancellationToken ct)
    {
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
                continue;
            }
            foreach (var sub in subdirs)
            {
                if (Path.GetFileName(sub).StartsWith('.')) continue;
                pending.Push(sub);
            }
            foreach (var file in files)
            {
                if (!extensions.Contains(Path.GetExtension(file))) continue;
                FileInfo info;
                try { info = new FileInfo(file); if (info.Length < MinSizeBytes) continue; }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { continue; }
                yield return new DiscoveredFile(PathNormalizer.Normalize(file), info.Length, info.LastWriteTimeUtc);
            }
        }
    }
}
