using System.Collections.Concurrent;

namespace Spacearr.Auth;

public sealed class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _failures = new();

    public bool IsLocked(string key)
    {
        if (!_failures.TryGetValue(key, out var entry)) return false;
        if (DateTime.UtcNow - entry.WindowStart > Window) { _failures.TryRemove(key, out _); return false; }
        return entry.Count >= MaxFailures;
    }

    public void RecordFailure(string key)
    {
        _failures.AddOrUpdate(key, _ => (1, DateTime.UtcNow), (_, e) =>
            DateTime.UtcNow - e.WindowStart > Window ? (1, DateTime.UtcNow) : (e.Count + 1, e.WindowStart));
    }

    public void Reset(string key) => _failures.TryRemove(key, out _);
}
