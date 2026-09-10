using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public sealed class FakeProber : IMediaProber
{
    public List<string> Probed { get; } = new();
    public Func<string, ProbeResult> Result { get; set; } = _ => new ProbeResult(3600, 1920, 1080, 23.976, "h264", "High", 8, 8_000_000, 7_800_000, "mkv", "AAC 2.0", 200_000, null);
    public HashSet<string> FailFor { get; } = new();
    public Dictionary<string, Func<Exception>> ThrowFor { get; } = new();

    public Task<ProbeResult> ProbeAsync(string path, CancellationToken ct)
    {
        lock (Probed) Probed.Add(path);
        var name = Path.GetFileName(path);
        if (ThrowFor.TryGetValue(name, out var makeException)) throw makeException();
        if (FailFor.Contains(name)) throw new ProbeException("corrupt");
        return Task.FromResult(Result(path));
    }
}
