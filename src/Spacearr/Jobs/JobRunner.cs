using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Jobs;

public sealed class JobRunner : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IProgressHub _hub;
    private readonly IClock _clock;
    private readonly ILogger<JobRunner> _log;

    public JobRunner(IJobQueue queue, IServiceScopeFactory scopes, IProgressHub hub, IClock clock, ILogger<JobRunner> log)
    { _queue = queue; _scopes = scopes; _hub = hub; _clock = clock; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _queue.MarkRunning(request.JobId, jobCts);
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var job = await db.Jobs.SingleAsync(j => j.Id == request.JobId, stoppingToken);
            job.Status = JobStatus.Running;
            job.StartedAt = _clock.UtcNow;
            await db.SaveChangesAsync(stoppingToken);

            JobStatus final;
            try
            {
                var instance = request.Factory(scope.ServiceProvider);
                var summary = await instance.RunAsync(new JobContext(job.Id, job.Type, scope.ServiceProvider, _hub), jobCts.Token);
                job.Summary = JsonSerializer.Serialize(summary, Json);
                final = JobStatus.Succeeded;
            }
            catch (OperationCanceledException) when (jobCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                final = JobStatus.Cancelled;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Job {JobId} ({Type}) failed", job.Id, job.Type);
                job.Error = ex.Message;
                final = JobStatus.Failed;
            }
            finally
            {
                _queue.MarkFinished(request.JobId);
            }

            job.Status = final;
            job.FinishedAt = _clock.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            _hub.Publish(new ProgressEvent(job.Id, job.Type, "finished", null, 0, 0, job.Error, final));
        }
    }
}
