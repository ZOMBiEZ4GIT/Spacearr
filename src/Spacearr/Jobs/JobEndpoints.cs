using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spacearr.Data;
using Spacearr.Data.Entities;

namespace Spacearr.Jobs;

public interface IJobFactories
{
    Func<IServiceProvider, IJob> Scan { get; }
    Func<IServiceProvider, IJob> Enrich { get; }
}

public sealed class JobFactories : IJobFactories
{
    public Func<IServiceProvider, IJob> Scan { get; init; } = _ => new NoOpJob(JobType.Scan);
    public Func<IServiceProvider, IJob> Enrich { get; init; } = _ => new NoOpJob(JobType.Enrich);
}

public sealed record JobResponse(int Id, JobType Type, JobStatus Status, JobTrigger Trigger, DateTime QueuedAt, DateTime? StartedAt, DateTime? FinishedAt, JobSummary? Summary, string? Error, ProgressEvent? Progress);
public sealed record PageResponse<T>(T[] Items, int Total, int Page, int PageSize);

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        group.MapPost("/jobs/scan", async (IJobQueue queue, IJobFactories factories) =>
            Results.Accepted(null, new { jobId = await queue.EnqueueAsync(JobType.Scan, JobTrigger.Manual, factories.Scan) }));

        group.MapPost("/jobs/enrich", async (IJobQueue queue, IJobFactories factories) =>
            Results.Accepted(null, new { jobId = await queue.EnqueueAsync(JobType.Enrich, JobTrigger.Manual, factories.Enrich) }));

        group.MapGet("/jobs", async (SpacearrDb db, IProgressHub hub, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
            var query = db.Jobs.AsNoTracking().OrderByDescending(j => j.Id);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Results.Ok(new PageResponse<JobResponse>(items.Select(j => ToResponse(j, hub)).ToArray(), total, page, pageSize));
        });

        group.MapGet("/jobs/{id:int}", async (int id, SpacearrDb db, IProgressHub hub) =>
        {
            var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id);
            return job is null ? Results.NotFound() : Results.Ok(ToResponse(job, hub));
        });

        group.MapPost("/jobs/{id:int}/cancel", (int id, IJobQueue queue) =>
            queue.TryCancel(id) ? Results.NoContent() : Results.Conflict(new { error = "Job is not running." }));

        group.MapGet("/events", async (HttpContext http, IProgressHub hub, CancellationToken ct) =>
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers["X-Accel-Buffering"] = "no";
            await http.Response.Body.FlushAsync(ct);

            // Single writer against the response body: instead of a second
            // background task racing the subscription loop for keepalive
            // comments (which could interleave partial writes on the same
            // stream), race one in-flight subscription read against a 15s
            // timer inside this one loop. `next` is only replaced once its
            // current value has actually been awaited, so at most one
            // MoveNextAsync is ever in flight on the enumerator at a time.
            await using var enumerator = hub.Subscribe(ct).GetAsyncEnumerator(ct);
            var next = enumerator.MoveNextAsync().AsTask();
            try
            {
                while (true)
                {
                    var delay = Task.Delay(TimeSpan.FromSeconds(15), ct);
                    var winner = await Task.WhenAny(next, delay);
                    if (winner == delay)
                    {
                        await http.Response.WriteAsync(": keepalive\n\n", ct);
                        await http.Response.Body.FlushAsync(ct);
                        continue;
                    }

                    if (!await next) break; // subscription ended
                    var e = enumerator.Current;
                    await http.Response.WriteAsync($"event: {e.Kind}\ndata: {JsonSerializer.Serialize(e, JobJson.Options)}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                    next = enumerator.MoveNextAsync().AsTask();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // Always observe the in-flight read before the enumerator is
                // disposed, so a disconnect never leaves an unawaited task.
                try { await next; } catch { /* channel closed or cancelled */ }
            }
        });

        return app;
    }

    private static JobResponse ToResponse(Job j, IProgressHub hub) => new(
        j.Id, j.Type, j.Status, j.Trigger, j.QueuedAt, j.StartedAt, j.FinishedAt,
        j.Summary is null ? null : JsonSerializer.Deserialize<JobSummary>(j.Summary, JobJson.Options),
        j.Error,
        j.Status == JobStatus.Running ? hub.LastFor(j.Id) : null);
}
