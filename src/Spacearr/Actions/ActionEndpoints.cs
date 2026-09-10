using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Data;
using Spacearr.Data.Entities;
using Spacearr.Jobs;

namespace Spacearr.Actions;

public static class ActionEndpoints
{
    public static IEndpointRouteBuilder MapActionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/actions").RequireAuthorization();

        group.MapPost("/preview", async (ActionRequest req, ActionPlanner planner, CancellationToken ct) =>
        {
            try { return Results.Ok(await planner.PlanAsync(req, ct)); }
            catch (ActionPlanException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (Spacearr.Arr.ArrException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/execute", async (ActionRequest req, ConfirmTokens tokens, ActionPlanner planner, IJobQueue queue, CancellationToken ct) =>
        {
            var clean = req with { ConfirmToken = null };
            if (!tokens.Validate(clean, req.ConfirmToken)) return Results.BadRequest(new { error = "Preview first: confirmation token missing or expired." });
            try { await planner.PlanAsync(clean, ct); } // re-validate against current state
            catch (ActionPlanException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (Spacearr.Arr.ArrException ex) { return Results.BadRequest(new { error = ex.Message }); }
            var jobId = await queue.EnqueueAsync(JobType.Action, JobTrigger.Manual, sp => ActivatorUtilities.CreateInstance<ActionJob>(sp, clean));
            return Results.Accepted(null, new { jobId });
        });

        group.MapGet("/log", async (SpacearrDb db, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 500);
            var q = db.ActionLogs.AsNoTracking().OrderByDescending(a => a.Id);
            return Results.Ok(new PageResponse<ActionLog>(await q.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(), await q.CountAsync(), page, pageSize));
        });

        return app;
    }
}
