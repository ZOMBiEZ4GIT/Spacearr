using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Jobs;

public class JobEndpointTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public JobEndpointTests(TestApp app) => _app = app;

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

    private sealed record EnqueueDto(int JobId);
    private sealed record JobDto(int Id, string Type, string Status, string Trigger);
    private sealed record PageDto<T>(T[] Items, int Total, int Page, int PageSize);
}
