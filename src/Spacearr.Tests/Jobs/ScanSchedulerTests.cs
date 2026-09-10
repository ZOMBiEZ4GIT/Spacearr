using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Settings;

namespace Spacearr.Tests.Jobs;

// Each test builds its own TestApp (and therefore its own temp SQLite db)
// rather than sharing one via IClassFixture, so seeded Job rows and the
// fake clock in one test can't leak into another.
public class ScanSchedulerTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class ClockTestApp : TestApp
    {
        public FakeClock Clock { get; } = new();
        protected override void ConfigureTestServices(IServiceCollection services) =>
            services.AddSingleton<IClock>(Clock);
    }

    private static ScanScheduler GetScheduler(TestApp app) =>
        app.Services.GetServices<IHostedService>().OfType<ScanScheduler>().Single();

    [Fact]
    public async Task No_finished_scan_enqueues_a_scheduled_scan()
    {
        using var app = new ClockTestApp();
        var scheduler = GetScheduler(app);

        await scheduler.TickAsync();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var count = await db.Jobs.CountAsync(j => j.Type == JobType.Scan && j.Trigger == JobTrigger.Scheduled);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Recent_finished_scan_within_interval_does_not_enqueue()
    {
        using var app = new ClockTestApp();
        app.Clock.UtcNow = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            // Default ScanIntervalHours is 6; this scan finished 1h ago, well within it.
            db.Jobs.Add(new Job
            {
                Type = JobType.Scan,
                Status = JobStatus.Succeeded,
                Trigger = JobTrigger.Scheduled,
                QueuedAt = app.Clock.UtcNow.AddHours(-1),
                StartedAt = app.Clock.UtcNow.AddHours(-1),
                FinishedAt = app.Clock.UtcNow.AddHours(-1),
            });
            await db.SaveChangesAsync();
        }

        var scheduler = GetScheduler(app);
        await scheduler.TickAsync();

        using var verifyScope = app.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var scanCount = await verifyDb.Jobs.CountAsync(j => j.Type == JobType.Scan);
        scanCount.Should().Be(1, "no new scan should have been enqueued");
    }

    [Fact]
    public async Task Interval_zero_disables_scheduling()
    {
        using var app = new ClockTestApp();

        using (var scope = app.Services.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var current = await settings.GetAsync();
            await settings.SaveAsync(current with { ScanIntervalHours = 0 });
        }

        var scheduler = GetScheduler(app);
        await scheduler.TickAsync();

        using var verifyScope = app.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var scanCount = await verifyDb.Jobs.CountAsync(j => j.Type == JobType.Scan);
        scanCount.Should().Be(0);
    }
}
