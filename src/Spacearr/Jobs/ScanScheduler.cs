using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Settings;

namespace Spacearr.Jobs;

public sealed class ScanScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IJobQueue _queue;
    private readonly IJobFactories _factories;
    private readonly IClock _clock;
    private readonly ILogger<ScanScheduler> _log;
    private readonly TimeSpan _poll;

    public ScanScheduler(IServiceScopeFactory scopes, IJobQueue queue, IJobFactories factories, IClock clock, ILogger<ScanScheduler> log, IHostEnvironment env)
    {
        _scopes = scopes; _queue = queue; _factories = factories; _clock = clock; _log = log;
        _poll = env.IsEnvironment("Testing") ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_poll);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await TickAsync(); }
            catch (Exception ex) { _log.LogWarning(ex, "Scheduler tick failed"); }
        }
    }

    internal async Task TickAsync()
    {
        using var scope = _scopes.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetAsync();
        if (settings.ScanIntervalHours <= 0) return;
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var last = await db.Jobs.Where(j => j.Type == JobType.Scan && j.FinishedAt != null)
            .OrderByDescending(j => j.FinishedAt).Select(j => j.FinishedAt).FirstOrDefaultAsync();
        if (last is null || _clock.UtcNow - last.Value >= TimeSpan.FromHours(settings.ScanIntervalHours))
            await _queue.EnqueueAsync(JobType.Scan, JobTrigger.Scheduled, _factories.Scan);
    }
}
