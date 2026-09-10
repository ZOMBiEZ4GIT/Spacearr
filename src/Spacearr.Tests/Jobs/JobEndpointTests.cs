using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data.Entities;
using Spacearr.Jobs;

namespace Spacearr.Tests.Jobs;

public class JobEndpointTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public JobEndpointTests(TestApp app) => _app = app;

    private sealed class SlowJob : IJob
    {
        // Action is never enqueued elsewhere in this fixture, so it can't
        // collide with another test's Scan/Enrich job for the single
        // JobRunner worker slot.
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
    public async Task Scan_enqueues_and_appears_in_list_and_detail()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var accepted = await client.PostAsync("/api/v1/jobs/scan", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await accepted.Content.ReadFromJsonAsync<EnqueueDto>();
        body!.JobId.Should().BeGreaterThan(0);

        var detail = await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{body.JobId}");
        detail!.Type.Should().Be("scan");

        var list = await client.GetFromJsonAsync<PageDto<JobDto>>("/api/v1/jobs?page=1&pageSize=10");
        list!.Items.Should().Contain(j => j.Id == body.JobId);
    }

    [Fact]
    public async Task Events_stream_sends_finished_event_for_a_job()
    {
        var client = await AuthedClient.CreateAsync(_app);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var enqueue = await client.PostAsync("/api/v1/jobs/enrich", null);
        var id = (await enqueue.Content.ReadFromJsonAsync<EnqueueDto>())!.JobId;

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
        string? line;
        var sawFinished = false;
        while ((line = await reader.ReadLineAsync(cts.Token)) is not null)
        {
            if (line.StartsWith("event: finished")) sawFinished = true;
            if (sawFinished && line.StartsWith("data:") && line.Contains($"\"jobId\":{id}")) break;
        }
        sawFinished.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_returns_204_for_running_job_and_409_for_finished_job()
    {
        var client = await AuthedClient.CreateAsync(_app);

        // Enqueue a job that stays Running long enough to cancel, directly
        // through the queue (the HTTP factories only wire up NoOpJob in this
        // task; Plan 2 replaces them with real, longer-running ones).
        var queue = _app.Services.GetRequiredService<IJobQueue>();
        var runningId = await queue.EnqueueAsync(JobType.Action, JobTrigger.Manual, _ => new SlowJob());
        await WaitUntilRunning(queue, runningId);

        var cancelRunning = await client.PostAsync($"/api/v1/jobs/{runningId}/cancel", null);
        cancelRunning.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WaitUntilFinished(client, runningId);

        // A job that has already finished can no longer be cancelled.
        var enqueue = await client.PostAsync("/api/v1/jobs/scan", null);
        var finishedId = (await enqueue.Content.ReadFromJsonAsync<EnqueueDto>())!.JobId;
        await WaitUntilFinished(client, finishedId);

        var cancelFinished = await client.PostAsync($"/api/v1/jobs/{finishedId}/cancel", null);
        cancelFinished.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task WaitUntilRunning(IJobQueue queue, int id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (queue.RunningJobId != id && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        queue.RunningJobId.Should().Be(id);
    }

    private static async Task WaitUntilFinished(HttpClient client, int id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var job = await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}");
            if (job!.Status is "succeeded" or "failed" or "cancelled") return;
            await Task.Delay(25);
        }
        throw new TimeoutException($"job {id} did not finish");
    }

    private sealed record EnqueueDto(int JobId);
    private sealed record JobDto(int Id, string Type, string Status, string Trigger);
    private sealed record PageDto<T>(T[] Items, int Total, int Page, int PageSize);
}
