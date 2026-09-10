using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Infrastructure;
using Spacearr.Library;

namespace Spacearr.Jobs;

public sealed class JobRunner : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IProgressHub _hub;
    private readonly IClock _clock;
    private readonly ILibraryCacheVersion _libraryCache;
    private readonly ILogger<JobRunner> _log;

    public JobRunner(IJobQueue queue, IServiceScopeFactory scopes, IProgressHub hub, IClock clock, ILibraryCacheVersion libraryCache, ILogger<JobRunner> log)
    { _queue = queue; _scopes = scopes; _hub = hub; _clock = clock; _libraryCache = libraryCache; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await RunOneAsync(request, stoppingToken);
        }
    }

    // Every failure mode here - a transient "database is locked" on the
    // initial load/write, an exception from the job itself, or a failure
    // persisting the terminal status - is caught and logged rather than
    // left to propagate out of ExecuteAsync. BackgroundServiceExceptionBehavior
    // defaults to StopHost, so letting any of these escape would take the
    // whole application down over one bad job; instead we mark the job
    // finished (best effort), publish a "finished" event where possible,
    // and move on to the next queued job.
    //
    // Scopes are deliberately split three ways: the runner's own bookkeeping
    // writes each use a short-lived scope that is disposed immediately, and
    // the job gets a scope of its own. That way the runner never holds a
    // SpacearrDb (and therefore a SQLite connection and change tracker) open
    // for the whole duration of a job, and the job's DbContext is never
    // shared with - or invalidated by - the runner's writes.
    private async Task RunOneAsync(JobRequest request, CancellationToken stoppingToken)
    {
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _queue.MarkRunning(request.JobId, jobCts);

        var final = JobStatus.Failed;
        string? error = null;

        try
        {
            using (var startScope = _scopes.CreateScope())
            {
                var startDb = startScope.ServiceProvider.GetRequiredService<SpacearrDb>();
                var job = await startDb.Jobs.SingleAsync(j => j.Id == request.JobId, stoppingToken);
                job.Status = JobStatus.Running;
                job.StartedAt = _clock.UtcNow;
                await startDb.SaveChangesAsync(stoppingToken);
            }

            string? summaryJson = null;
            using (var jobScope = _scopes.CreateScope())
            {
                try
                {
                    var instance = request.Factory(jobScope.ServiceProvider);
                    var summary = await instance.RunAsync(new JobContext(request.JobId, request.Type, jobScope.ServiceProvider, _hub), jobCts.Token);
                    summaryJson = JsonSerializer.Serialize(summary, JobJson.Options);
                    final = JobStatus.Succeeded;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    final = JobStatus.Failed;
                    error = "Interrupted by shutdown";
                }
                catch (OperationCanceledException) when (jobCts.IsCancellationRequested)
                {
                    final = JobStatus.Cancelled;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Job {JobId} ({Type}) failed", request.JobId, request.Type);
                    error = ex.Message;
                    final = JobStatus.Failed;
                }
            }

            // Any job may have changed the library, so invalidate the cached library
            // queries now the job scope is closed - deliberately before the terminal
            // status is written, so nothing watching for the job to finish (the UI, a
            // test polling /jobs/{id}) can see "finished" and still be served a cache
            // entry built before the job ran.
            _libraryCache.Bump();

            try
            {
                using var endScope = _scopes.CreateScope();
                var endDb = endScope.ServiceProvider.GetRequiredService<SpacearrDb>();
                var job = await endDb.Jobs.SingleAsync(j => j.Id == request.JobId, CancellationToken.None);
                job.Status = final;
                job.Summary = summaryJson;
                job.Error = error;
                job.FinishedAt = _clock.UtcNow;
                await endDb.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The DB write failed (e.g. transient lock). The event below
                // still reports the outcome the job actually reached; a
                // restart will additionally reconcile any row left Running.
                _log.LogError(ex, "Job {JobId} ({Type}) failed to persist terminal status {Status}", request.JobId, request.Type, final);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Job {JobId} ({Type}) runner failure", request.JobId, request.Type);
            final = JobStatus.Failed;
            error = ex.Message;
        }
        finally
        {
            _queue.MarkFinished(request.JobId);
        }

        _hub.Publish(new ProgressEvent(request.JobId, request.Type, "finished", null, 0, 0, error, final));
    }
}
