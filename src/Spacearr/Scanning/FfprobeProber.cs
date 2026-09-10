using System.Diagnostics;

namespace Spacearr.Scanning;

public interface IMediaProber
{
    Task<ProbeResult> ProbeAsync(string path, CancellationToken ct);
}

public sealed class FfprobeProber : IMediaProber
{
    private readonly IToolLocator _tools;
    public FfprobeProber(IToolLocator tools) => _tools = tools;

    public async Task<ProbeResult> ProbeAsync(string path, CancellationToken ct)
    {
        var exe = _tools.Ffprobe ?? throw new ProbeException("ffprobe was not found. Install ffmpeg or set the ffprobe path in Settings.");
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var a in new[] { "-v", "error", "-print_format", "json", "-show_format", "-show_streams", path }) psi.ArgumentList.Add(a);
        ct.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = psi };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            process.Start();
            // Process.Dispose does not close redirected streams once they've been read
            // through StandardOutput/StandardError - it leaves them to the caller - so
            // they must be disposed here or each probe leaks two pipe handles until a GC
            // finalises them, which exhausts a 1024 fd limit on a large library.
            using var stdoutReader = process.StandardOutput;
            using var stderrReader = process.StandardError;
            var stdout = stdoutReader.ReadToEndAsync(timeout.Token);
            var stderr = stderrReader.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0) throw new ProbeException($"ffprobe exit {process.ExitCode}: {error.Trim()}");
            return FfprobeParser.Parse(output);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (ct.IsCancellationRequested) throw; // caller cancelled: propagate as-is
            throw new ProbeException("ffprobe timed out after 60 s");
        }
        catch (FormatException ex) { throw new ProbeException(ex.Message, ex); }
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(true); } catch { }
    }
}
