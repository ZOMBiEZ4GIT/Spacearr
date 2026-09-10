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
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.AddSingleton<IMediaProber>(_prober);
            // Registered for every test (not just the one that uses it): with
            // SkipEnabled left false it behaves exactly like the real
            // FileDiscovery, so this is a no-op for tests that never touch it.
            services.AddSingleton<FlakyFileDiscovery>();
            services.AddSingleton<IFileDiscovery>(sp => sp.GetRequiredService<FlakyFileDiscovery>());
        }
    }

    public void Dispose()
    {
        _app.Dispose();
        try { Directory.Delete(_root, true); } catch { /* best effort - a test may already have removed it */ }
    }

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

    private async Task<RootFolder> GetRoot()
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SpacearrDb>().RootFolders.AsNoTracking().SingleAsync();
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

    [Fact]
    public async Task Missing_root_this_run_does_not_delete_its_files_or_touch_LastScanAt()
    {
        await AddRoot();
        Make("a.mkv", 2_000_000);
        Make("b.mkv", 2_000_000);

        var first = await RunScan();
        first.FilesAdded.Should().Be(2);
        var lastScanAt = (await GetRoot()).LastScanAt;
        lastScanAt.Should().NotBeNull();

        Directory.Delete(_root, true);

        var second = await RunScan();
        second.FilesRemoved.Should().Be(0);
        second.Errors.Should().Contain(e => e.StartsWith("Root folder not found"));
        (await Files()).Should().HaveCount(2);
        (await GetRoot()).LastScanAt.Should().Be(lastScanAt);
    }

    [Fact]
    public async Task Overlapping_roots_dedupe_the_same_file_instead_of_crashing()
    {
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            db.RootFolders.Add(new RootFolder { Path = PathNormalizer.Normalize(_root) });
            db.RootFolders.Add(new RootFolder { Path = PathNormalizer.Normalize(Path.Combine(_root, "sub")) });
            await db.SaveChangesAsync();
        }
        Make("sub/x.mkv", 2_000_000);

        var result = await RunScan();
        result.FilesSeen.Should().Be(1);
        (await Files()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Non_probe_exceptions_are_recorded_per_file_without_aborting_the_scan()
    {
        await AddRoot();
        Make("a.mkv", 2_000_000);
        Make("bad.mkv", 2_000_000);
        _prober.ThrowFor["bad.mkv"] = () => new IOException("disk read error");

        var result = await RunScan();
        result.FilesProbed.Should().Be(2);
        var files = await Files();
        files.Single(f => f.Path.EndsWith("bad.mkv")).ProbeError.Should().Be("disk read error");
        files.Single(f => f.Path.EndsWith("a.mkv")).ProbeError.Should().BeNull();
        files.Single(f => f.Path.EndsWith("a.mkv")).VideoCodec.Should().Be("h264");
    }

    [Fact]
    public async Task Fallback_video_bitrate_is_computed_from_size_duration_and_audio_bitrate()
    {
        await AddRoot();
        Make("a.mkv", 40_000_000);
        _prober.Result = _ => new ProbeResult(100, 1920, 1080, 23.976, "h264", "High", 8, 8_000_000, null, "mkv", "AAC 2.0", 128_000, null);

        await RunScan();
        var file = (await Files()).Single();
        file.VideoBitrateBps.Should().Be(40_000_000L * 8 / 100 - 128_000);
    }

    [Fact]
    public async Task Unreadable_subdirectory_excludes_its_root_from_cleanup_but_not_from_LastScanAt()
    {
        await AddRoot();
        Make("a.mkv", 2_000_000);
        Make("sub/b.mkv", 2_000_000);

        var first = await RunScan();
        first.FilesAdded.Should().Be(2);

        var discovery = _app.Services.GetRequiredService<FlakyFileDiscovery>();
        discovery.SkipEnabled = true;
        discovery.SkipSubdirectory = Path.Combine(_root, "sub");

        var beforeSecond = DateTime.UtcNow;
        var second = await RunScan();
        second.FilesRemoved.Should().Be(0);
        second.Errors.Should().Contain(e => e.Contains("Skipped cleanup"));
        (await Files()).Select(f => Path.GetFileName(f.Path)).Should().Contain("b.mkv");
        // Cleanup was skipped, but the root was still walked: LastScanAt must still be
        // stamped by this run, otherwise an unreadable subdirectory would leave the root
        // looking as though it had never been scanned.
        var scanned = (await GetRoot()).LastScanAt;
        scanned.Should().NotBeNull();
        scanned!.Value.Should().BeOnOrAfter(beforeSecond);

        // The guard must not disable cleanup permanently: once the subdirectory
        // is readable again (flag off) and the file is genuinely gone, cleanup
        // removes it as usual.
        discovery.SkipEnabled = false;
        File.Delete(Path.Combine(_root, "sub", "b.mkv"));

        var third = await RunScan();
        third.FilesRemoved.Should().Be(1);
        (await Files()).Select(f => Path.GetFileName(f.Path)).Should().NotContain("b.mkv");
    }
}
