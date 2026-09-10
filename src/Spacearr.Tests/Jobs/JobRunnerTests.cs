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
        var first = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new CountingJob());
        var second = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, _ => new CountingJob());
        second.Should().Be(first);
        await WaitForFinish(first);
    }

    private async Task WaitForFinish(int id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _app.Services.CreateScope();
            var job = await scope.ServiceProvider.GetRequiredService<SpacearrDb>().Jobs.AsNoTracking().SingleAsync(j => j.Id == id);
            if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled) return;
            await Task.Delay(25);
        }
        throw new TimeoutException($"job {id} did not finish");
    }
}
