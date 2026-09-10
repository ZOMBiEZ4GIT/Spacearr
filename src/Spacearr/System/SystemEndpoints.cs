using System.Reflection;

namespace Spacearr.System;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/system/status", async (Spacearr.Auth.IUserService users) =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            return Results.Ok(new StatusResponse(version, await users.IsSetupCompleteAsync(), new ToolsStatus(false, false)));
        }).AllowAnonymous().WithName("SystemStatus");
        return app;
    }
}

public sealed record StatusResponse(string Version, bool SetupComplete, ToolsStatus Tools);
public sealed record ToolsStatus(bool Ffprobe, bool Mediainfo);
