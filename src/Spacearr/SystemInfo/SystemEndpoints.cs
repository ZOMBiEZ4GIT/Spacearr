using System.Reflection;
using Spacearr.Auth;
using Spacearr.Scanning;

namespace Spacearr.SystemInfo;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/system/status", async (IUserService users, IToolLocator tools) =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            return Results.Ok(new StatusResponse(version, await users.IsSetupCompleteAsync(),
                new ToolsStatus(tools.Ffprobe is not null, tools.Mediainfo is not null)));
        }).AllowAnonymous().WithName("SystemStatus");
        return app;
    }
}

public sealed record StatusResponse(string Version, bool SetupComplete, ToolsStatus Tools);
public sealed record ToolsStatus(bool Ffprobe, bool Mediainfo);
