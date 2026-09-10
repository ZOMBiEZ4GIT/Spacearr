namespace Spacearr.Library;

/// <summary>
/// A monotonically increasing counter that forms part of every cached library
/// query key. Nothing evicts library cache entries directly: a job that changes
/// the library (scan, enrich, action) bumps this instead, which makes every
/// previously cached key unreachable in one step - no key bookkeeping, and no
/// window where a stale entry survives because we forgot to enumerate it.
/// </summary>
public interface ILibraryCacheVersion
{
    int Current { get; }
    void Bump();
}

public sealed class LibraryCacheVersion : ILibraryCacheVersion
{
    private int _current;
    public int Current => Volatile.Read(ref _current);
    public void Bump() => Interlocked.Increment(ref _current);
}
