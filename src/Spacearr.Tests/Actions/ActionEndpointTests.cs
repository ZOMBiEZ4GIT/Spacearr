using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Tests.Arr;
using Spacearr.Tests.Library;

namespace Spacearr.Tests.Actions;

public class ActionEndpointTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public ActionEndpointTests(ArrTestApp app) => _app = app;

    private async Task<int> ItemMatchingFixtureMovie()
    {
        // Seed one item that points at the fixture movie (id 10, file 77) so the fake arr handler answers.
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
        var (instanceId, _) = await Seed.LibraryAsync(_app, movies: 1);
        var item = await db.MediaItems.SingleAsync(i => i.ArrInstanceId == instanceId);
        item.ExternalId = 10; item.ArrFileId = 77; item.Title = "Film";
        await db.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task Execute_without_preview_is_rejected()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemId = await ItemMatchingFixtureMovie();
        var r = await client.PostAsJsonAsync("/api/v1/actions/execute", new { type = "delete", itemId });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("Preview first");
    }

    [Fact]
    public async Task Replace_preview_lists_steps_then_execute_runs_them_in_order_and_logs()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemId = await ItemMatchingFixtureMovie();
        _app.Arr.Calls.Clear();

        var preview = await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "replace", itemId, targetProfileId = 4 });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var p = await preview.Content.ReadFromJsonAsync<JsonElement>();
        p.GetProperty("steps").GetArrayLength().Should().Be(3);
        p.GetProperty("bytesFreedNow").GetInt64().Should().BeGreaterThan(0);
        var token = p.GetProperty("confirmToken").GetString();

        var exec = await client.PostAsJsonAsync("/api/v1/actions/execute", new { type = "replace", itemId, targetProfileId = 4, confirmToken = token });
        exec.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var jobId = (await exec.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt32();

        await WaitForJob(client, jobId);
        var calls = _app.Arr.Calls.Where(c => c.Method != HttpMethod.Get).Select(c => $"{c.Method} {c.Path}").ToList();
        calls.Should().ContainInOrder("PUT /api/v3/movie/10", "DELETE /api/v3/moviefile/77", "POST /api/v3/command");

        var log = await client.GetFromJsonAsync<JsonElement>("/api/v1/actions/log");
        var entry = log.GetProperty("items").EnumerateArray().First();
        entry.GetProperty("outcome").GetString().Should().Be("succeeded");
        entry.GetProperty("qualityAfter").GetString().Should().Be("HD-1080p");
    }

    [Fact]
    public async Task Preview_rejects_same_profile_and_unknown_item()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemId = await ItemMatchingFixtureMovie();
        (await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "replace", itemId, targetProfileId = 5 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "delete", itemId = 999999 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_with_unmonitor_deletes_through_arr_removes_file_row_and_logs()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemId = await ItemMatchingFixtureMovie();
        _app.Arr.Calls.Clear();

        int fileId;
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            fileId = (await db.MediaItems.SingleAsync(i => i.Id == itemId)).MediaFileId!.Value;
        }

        var preview = await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "delete", itemId, unmonitor = true });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmToken").GetString();

        var exec = await client.PostAsJsonAsync("/api/v1/actions/execute", new { type = "delete", itemId, unmonitor = true, confirmToken = token });
        exec.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var jobId = (await exec.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt32();

        await WaitForJob(client, jobId);

        var calls = _app.Arr.Calls.Where(c => c.Method != HttpMethod.Get).ToList();
        calls.Select(c => $"{c.Method} {c.Path}").Should().ContainInOrder("DELETE /api/v3/moviefile/77", "PUT /api/v3/movie/10");
        calls.Single(c => c.Path == "/api/v3/movie/10").Body.Should().Contain("\"monitored\":false");

        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var item = await db.MediaItems.SingleAsync(i => i.Id == itemId);
            item.MediaFileId.Should().BeNull();
            item.ArrFileId.Should().BeNull();
            (await db.MediaFiles.FindAsync(fileId)).Should().BeNull();
        }

        var log = await client.GetFromJsonAsync<JsonElement>("/api/v1/actions/log");
        log.GetProperty("items").EnumerateArray().First().GetProperty("outcome").GetString().Should().Be("succeeded");
    }

    [Fact]
    public async Task Forged_or_cross_item_token_is_rejected()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var itemIdA = await ItemMatchingFixtureMovie();
        var (_, otherIds) = await Seed.LibraryAsync(_app, movies: 1);
        var itemIdB = otherIds[0];

        var preview = await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "delete", itemId = itemIdA });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmToken").GetString();

        var exec = await client.PostAsJsonAsync("/api/v1/actions/execute", new { type = "delete", itemId = itemIdB, confirmToken = token });
        exec.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await exec.Content.ReadAsStringAsync()).Should().Contain("Preview first");
    }

    [Fact]
    public async Task Failure_after_delete_still_removes_file_row_and_logs_partial()
    {
        using var dedicated = new ArrTestApp();
        dedicated.Arr.Map("POST", "/api/v3/command", """{"error":"boom"}""", HttpStatusCode.InternalServerError);
        var client = await AuthedClient.CreateAsync(dedicated);

        int itemId, fileId;
        using (var scope = dedicated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            var (instanceId, _) = await Seed.LibraryAsync(dedicated, movies: 1);
            var item = await db.MediaItems.SingleAsync(i => i.ArrInstanceId == instanceId);
            item.ExternalId = 10; item.ArrFileId = 77; item.Title = "Film";
            await db.SaveChangesAsync();
            itemId = item.Id;
            fileId = item.MediaFileId!.Value;
        }

        var preview = await client.PostAsJsonAsync("/api/v1/actions/preview", new { type = "replace", itemId, targetProfileId = 4 });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmToken").GetString();

        var exec = await client.PostAsJsonAsync("/api/v1/actions/execute", new { type = "replace", itemId, targetProfileId = 4, confirmToken = token });
        exec.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var jobId = (await exec.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetInt32();

        await WaitForJob(client, jobId, expected: "failed");

        var log = await client.GetFromJsonAsync<JsonElement>("/api/v1/actions/log");
        var entry = log.GetProperty("items").EnumerateArray().First();
        entry.GetProperty("outcome").GetString().Should().Be("failed");
        var detail = entry.GetProperty("detail").GetString();
        detail.Should().Contain("profile changed").And.Contain("file deleted");

        using (var scope = dedicated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
            (await db.MediaFiles.FindAsync(fileId)).Should().BeNull();
        }
    }

    internal static async Task WaitForJob(HttpClient client, int jobId, string expected = "succeeded")
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var job = await client.GetFromJsonAsync<JsonElement>($"/api/v1/jobs/{jobId}");
            var status = job.GetProperty("status").GetString();
            if (status is "succeeded" or "failed" or "cancelled") { status.Should().Be(expected, job.ToString()); return; }
            await Task.Delay(50);
        }
        throw new TimeoutException();
    }
}
