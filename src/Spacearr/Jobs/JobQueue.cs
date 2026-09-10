using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Jobs;

public sealed record JobRequest(int JobId, JobType Type, Func<IServiceProvider, IJob> Factory);

public interface IJobQueue
{
    Task<int> EnqueueAsync(JobType type, JobTrigger trigger, Func<IServiceProvider, IJob> factory);
    bool TryCancel(int jobId);
    int? RunningJobId { get; }
    ChannelReader<JobRequest> Reader { get; }
    void MarkRunning(int jobId, CancellationTokenSource cts);
    void MarkFinished(int jobId);
}

public sealed class JobQueue : IJobQueue
{
    private readonly Channel<JobRequest> _channel = Channel.CreateUnbounded<JobRequest>();
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _enqueueLock = new(1, 1);
    private int? _running;
    private CancellationTokenSource? _runningCts;

    public JobQueue(IServiceScopeFactory scopes, IClock clock) { _scopes = scopes; _clock = clock; }

    public ChannelReader<JobRequest> Reader => _channel.Reader;
    public int? RunningJobId { get { lock (_gate) return _running; } }

    public async Task<int> EnqueueAsync(JobType type, JobTrigger trigger, Func<IServiceProvider, IJob> factory)
    {
        // Serialise enqueue calls so two concurrent Scan (or Enrich) requests
        // can't both miss each other's uncommitted "is one already
        // queued/running?" check and create duplicate rows.
        await _enqueueLock.WaitAsync();
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            if (type is JobType.Scan or JobType.Enrich)
            {
                var existing = await db.Jobs.Where(j => j.Type == type && (j.Status == JobStatus.Queued || j.Status == JobStatus.Running))
                    .OrderBy(j => j.Id).Select(j => (int?)j.Id).FirstOrDefaultAsync();
                if (existing is not null) return existing.Value;
            }
            var job = new Job { Type = type, Status = JobStatus.Queued, Trigger = trigger, QueuedAt = _clock.UtcNow };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            await _channel.Writer.WriteAsync(new JobRequest(job.Id, type, factory));
            return job.Id;
        }
        finally
        {
            _enqueueLock.Release();
        }
    }

    public bool TryCancel(int jobId)
    {
        lock (_gate)
        {
            if (_running == jobId && _runningCts is not null) { _runningCts.Cancel(); return true; }
        }
        return false;
    }

    public void MarkRunning(int jobId, CancellationTokenSource cts) { lock (_gate) { _running = jobId; _runningCts = cts; } }
    public void MarkFinished(int jobId) { lock (_gate) { if (_running == jobId) { _running = null; _runningCts = null; } } }
}
