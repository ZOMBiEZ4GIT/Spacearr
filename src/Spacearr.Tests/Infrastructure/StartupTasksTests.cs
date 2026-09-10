using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Tests.Infrastructure;

public class StartupTasksTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spacearr-startup", Guid.NewGuid().ToString("N"));

    public StartupTasksTests() => Directory.CreateDirectory(_dir);

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SpacearrDb>(o => o.UseSqlite($"Data Source={Path.Combine(_dir, "spacearr.db")};Default Timeout=30"));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task First_run_migrates_and_records_the_app_version()
    {
        using var provider = BuildServices();
        await StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        var stored = await db.Settings.FindAsync("schema.appVersion");
        stored!.Value.Should().Be("1.2.3");
    }

    [Fact]
    public async Task A_database_written_by_a_newer_build_refuses_to_start()
    {
        using var provider = BuildServices();
        await StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));
        await SetStoredVersionAsync(provider, "99.0.0");

        var act = () => StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*newer*");
    }

    [Fact]
    public async Task An_older_stored_version_is_overwritten_with_the_current_one()
    {
        using var provider = BuildServices();
        await StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));
        await SetStoredVersionAsync(provider, "0.0.1");

        await StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var stored = await db.Settings.FindAsync("schema.appVersion");
        stored!.Value.Should().Be("1.2.3");
    }

    [Fact]
    public async Task A_job_left_running_by_a_crash_is_reconciled_as_failed()
    {
        using var provider = BuildServices();
        await StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));

        int jobId;
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var job = new Job { Type = JobType.Scan, Status = JobStatus.Running, Trigger = JobTrigger.Manual, QueuedAt = DateTime.UtcNow };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;
        }

        await StartupTasks.MigrateAndGuardAsync(provider, new Version(1, 2, 3));

        using var scope = provider.CreateScope();
        var after = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        after.Status.Should().Be(JobStatus.Failed);
        after.Error.Should().Be("Interrupted by restart");
        after.FinishedAt.Should().NotBeNull();
    }

    private static async Task SetStoredVersionAsync(IServiceProvider provider, string version)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var row = await db.Settings.FindAsync("schema.appVersion");
        row!.Value = version;
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }
}
