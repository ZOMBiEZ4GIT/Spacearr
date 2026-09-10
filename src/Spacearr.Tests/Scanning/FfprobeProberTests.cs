using System.Diagnostics;
using FluentAssertions;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public class FfprobeProberTests
{
    private sealed class FixedTools : IToolLocator
    {
        public string? Ffprobe { get; init; }
        public string? Mediainfo => null;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Missing_ffprobe_throws_probe_exception_with_guidance()
    {
        var prober = new FfprobeProber(new FixedTools { Ffprobe = null });
        var act = () => prober.ProbeAsync("x.mkv", CancellationToken.None);
        await act.Should().ThrowAsync<ProbeException>().WithMessage("*Install ffmpeg*");
    }

    [Fact]
    public async Task Real_ffprobe_on_generated_file_when_available()
    {
        var (ffprobe, ffmpeg) = ResolveTools();
        if (ffprobe is null || ffmpeg is null) return; // gated: tool not installed on this machine

        var file = await GenerateSampleFileAsync(ffmpeg);
        try
        {
            var r = await new FfprobeProber(new FixedTools { Ffprobe = ffprobe }).ProbeAsync(file, CancellationToken.None);
            r.VideoCodec.Should().Be("h264");
            r.Width.Should().Be(320);
            r.FrameRate.Should().BeApproximately(25, 0.01);
            r.DurationSeconds.Should().BeApproximately(2, 0.2);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Already_cancelled_token_throws_cancellation_not_probe_exception()
    {
        var (ffprobe, ffmpeg) = ResolveTools();
        if (ffprobe is null || ffmpeg is null) return; // gated: tool not installed on this machine

        var file = await GenerateSampleFileAsync(ffmpeg);
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var prober = new FfprobeProber(new FixedTools { Ffprobe = ffprobe });
            var act = () => prober.ProbeAsync(file, cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Cancelling_after_start_kills_the_ffprobe_process()
    {
        var (ffprobe, ffmpeg) = ResolveTools();
        if (ffprobe is null || ffmpeg is null) return; // gated: tool not installed on this machine

        var file = await GenerateSampleFileAsync(ffmpeg);
        try
        {
            var baseline = Process.GetProcessesByName("ffprobe").Length;

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            var prober = new FfprobeProber(new FixedTools { Ffprobe = ffprobe });
            try { await prober.ProbeAsync(file, cts.Token); }
            catch (OperationCanceledException) { /* expected: caller-cancelled */ }

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (Process.GetProcessesByName("ffprobe").Length > baseline && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Process.GetProcessesByName("ffprobe").Length.Should().Be(baseline, "no ffprobe process should be left running after caller cancellation");
        }
        finally { File.Delete(file); }
    }

    private static (string? Ffprobe, string? Ffmpeg) ResolveTools() =>
        (ToolLocator.Resolve(null, "ffprobe"), ToolLocator.Resolve(null, "ffmpeg"));

    private static async Task<string> GenerateSampleFileAsync(string ffmpeg)
    {
        var file = Path.Combine(Path.GetTempPath(), $"spacearr-{Guid.NewGuid():N}.mp4");
        var gen = Process.Start(new ProcessStartInfo(ffmpeg)
        {
            ArgumentList = { "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25", "-t", "2", "-c:v", "libx264", "-b:v", "500k", file },
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        })!;
        // ffmpeg writes progress continuously to stderr; the pipe must be drained
        // concurrently with WaitForExitAsync or the process deadlocks once the
        // OS pipe buffer fills.
        var genStderr = gen.StandardError.ReadToEndAsync();
        await gen.WaitForExitAsync();
        await genStderr;
        return file;
    }
}
