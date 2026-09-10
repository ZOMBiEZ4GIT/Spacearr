using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Jobs;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public class ScanJobTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spacearr-scan-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProber _prober = new();
    private readonly TestApp _app;

    public ScanJobTests()
    {
        Directory.CreateDirectory(_root);
        _app = new ScanTestApp(_prober);
    }

    private sealed class ScanTestApp : TestApp
    {
        private readonly FakeProber _prober;
        public ScanTestApp(FakeProber prober) => _prober = prober;
        protected override void ConfigureTestServices(IServiceCollection services) =>
            services.AddSingleton<IMediaProber>(_prober);
    }

    public void Dispose() { _app.Dispose(); Directory.Delete(_root, true); }

    private void Make(string relative, int size)
    {
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[size]);
    }

    private async Task<JobSummary> RunScan()
    {
        using var scope = _app.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ScanJob>();
        var ctx = new JobContext(0, JobType.Scan, scope.ServiceProvider, _app.Services.GetRequiredService<IProgressHub>());
        return await job.RunAsync(ctx, CancellationToken.None);
    }

    private async Task AddRoot()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        db.RootFolders.Add(new RootFolder { Path = PathNormalizer.Normalize(_root) });
        await db.SaveChangesAsync();
    }

    private async Task<List<MediaFile>> Files()
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SpacearrDb>().MediaFiles.AsNoTracking().OrderBy(f => f.Path).ToListAsync();
    }

    [Fact]
    public async Task First_scan_probes_everything_second_scan_probes_only_changes_and_removes_orphans()
    {
        await AddRoot();
        Make("a.mkv", 2_000_000);
        Make("b.mkv", 2_000_000);
        Make("bad.mkv", 2_000_000);
        _prober.FailFor.Add("bad.mkv");

        var first = await RunScan();
        first.FilesSeen.Should().Be(3);
        first.FilesProbed.Should().Be(3);
        first.FilesAdded.Should().Be(3);
        var files = await Files();
        files.Should().HaveCount(3);
        files.Single(f => f.Path.EndsWith("bad.mkv")).ProbeError.Should().Be("corrupt");
        files.Single(f => f.Path.EndsWith("a.mkv")).VideoCodec.Should().Be("h264");

        _prober.Probed.Clear();
        File.Delete(Path.Combine(_root, "b.mkv"));
        Make("a.mkv", 2_500_000);
        Make("c.mkv", 2_000_000);

        var second = await RunScan();
        second.FilesSeen.Should().Be(3);
        second.FilesProbed.Should().Be(3); // a changed, c new, bad retried
        second.FilesAdded.Should().Be(1);
        second.FilesRemoved.Should().Be(1);
        _prober.Probed.Should().NotContain(p => p.EndsWith("b.mkv"));
        (await Files()).Select(f => Path.GetFileName(f.Path)).Should().BeEquivalentTo("a.mkv", "bad.mkv", "c.mkv");
    }

    [Fact]
    public async Task Disabled_roots_are_skipped()
    {
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            db.RootFolders.Add(new RootFolder { Path = PathNormalizer.Normalize(_root), Enabled = false });
            await db.SaveChangesAsync();
        }
        Make("a.mkv", 2_000_000);
        var s = await RunScan();
        s.FilesSeen.Should().Be(0);
    }
}
