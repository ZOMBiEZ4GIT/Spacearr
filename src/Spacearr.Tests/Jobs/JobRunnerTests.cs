using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Jobs;

namespace Spacearr.Tests.Jobs;

public class JobRunnerTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public JobRunnerTests(TestApp app) => _app = app;

    private sealed class CountingJob : IJob
    {
        public JobType Type => JobType.Scan;
        public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
        {
            for (var i = 1; i <= 3; i++)
            {
                ct.ThrowIfCancellationRequested();
                ctx.Report("counting", i, 3);
                await Task.Delay(10, ct);
            }
            return new JobSummary(FilesSeen: 3);
        }
    }

    private sealed class FailingJob : IJob
    {
        public JobType Type => JobType.Enrich;
        public Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    /// <summary>
    /// Blocks until the test releases it, so the job is guaranteed to still be
    /// queued/running while the second enqueue is made - the dedupe check being
    /// tested only applies to a job that has not finished yet.
    /// </summary>
    private sealed class GatedJob : IJob
    {
        private readonly TaskCompletionSource _gate;
        public GatedJob(TaskCompletionSource gate) => _gate = gate;
        public JobType Type => JobType.Scan;
        public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
        {
            await _gate.Task.WaitAsync(ct);
            return new JobSummary();
        }
    }

    private sealed class SlowJob : IJob
    {
        // Action is unused by any other test in this file, so it never
        // collides with the Scan/Enrich dedupe check or another test's job.
        public JobType Type => JobType.Action;
        public async Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }
        }
    }

    [Fact]
    public async Task Runs_job_records_status_summary_and_publishes_progress()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var hub = _app.Services.GetRequiredService<IProgressHub>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var events = new List<ProgressEvent>();
        var listener = Task.Run(async () =>
        {
            await foreach (var e in hub.Subscribe(cts.Token))
            {
                events.Add(e);
                if (e.Kind == "finished" && e.Type == JobType.Scan) break;
            }
        });

        var id = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new CountingJob());
        await listener;

        using var scope = _app.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.SingleAsync(j => j.Id == id);
        job.Status.Should().Be(JobStatus.Succeeded);
        job.StartedAt.Should().NotBeNull();
        job.FinishedAt.Should().NotBeNull();
        job.Summary.Should().Contain("\"filesSeen\":3");
        events.Count(e => e.Kind == "progress" && e.JobId == id).Should().Be(3);
        events.Last().Status.Should().Be(JobStatus.Succeeded);
    }

    [Fact]
    public async Task Failed_job_records_error()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var id = await queue.EnqueueAsync(JobType.Enrich, JobTrigger.Manual, _ => new FailingJob());
        await WaitForFinish(id);
        using var scope = _app.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.SingleAsync(j => j.Id == id);
        job.Status.Should().Be(JobStatus.Failed);
        job.Error.Should().Contain("boom");
    }

    [Fact]
    public async Task Duplicate_scan_enqueue_returns_existing_id()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = 0;
        try
        {
            first = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new GatedJob(gate));
            var second = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new GatedJob(gate));
            second.Should().Be(first, "a scan is already queued or running, so the second enqueue must reuse it");
        }
        finally
        {
            gate.TrySetResult();
        }
        await WaitForFinish(first);
    }

    [Fact]
    public async Task Cancelling_a_running_job_marks_it_cancelled()
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var id = await queue.EnqueueAsync(JobType.Action, JobTrigger.Manual, _ => new SlowJob());

        await WaitUntilRunning(queue, id);

        queue.TryCancel(id).Should().BeTrue();
        await WaitForFinish(id);

        using var scope = _app.Services.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.SingleAsync(j => j.Id == id);
        job.Status.Should().Be(JobStatus.Cancelled);
    }

    // 30 s, not 10: on a loaded runner (a 2-CPU CI box, or a host mid-rescan) nine
    // WebApplicationFactory hosts start in parallel and a job that needs ~50 ms of CPU
    // can wait far longer than that for a slice. A busy machine should read as slow,
    // not red - the assertions below still catch a job that never finishes.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    private static async Task WaitUntilRunning(IJobQueue queue, int id)
    {
        var deadline = DateTime.UtcNow + Deadline;
        while (queue.RunningJobId != id && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        queue.RunningJobId.Should().Be(id);
    }

    private async Task WaitForFinish(int id)
    {
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var deadline = DateTime.UtcNow + Deadline;
        while (DateTime.UtcNow < deadline)
        {
            // Ask the queue first: while the job is the running one there is nothing to
            // read from the database yet, so don't spend a SQLite round-trip on it.
            if (queue.RunningJobId == id) { await Task.Delay(25); continue; }
            using var scope = _app.Services.CreateScope();
            var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.AsNoTracking().SingleAsync(j => j.Id == id);
            if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) return;
            await Task.Delay(25);
        }
        throw new TimeoutException($"job {id} did not finish within {Deadline.TotalSeconds:0} s");
    }
}
