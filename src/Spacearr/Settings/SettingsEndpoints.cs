using System.Security.Claims;
using Spacearr.Auth;
using Spacearr.Scanning;

namespace Spacearr.Settings;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings").RequireAuthorization();
        group.MapGet("/", async (ISettingsService settings) => Results.Ok(await settings.GetAsync()));
        group.MapPut("/", async (AppSettings incoming, ClaimsPrincipal principal, ISettingsService settings, IToolLocator tools) =>
        {
            var error = incoming.Validate();
            if (error is not null) return Results.BadRequest(new { error });

            // Tool paths name an executable Spacearr will run, so changing one
            // is a privileged operation: an API key alone must not be enough,
            // and the path has to actually exist as a file.
            var current = await settings.GetAsync();
            var changed = new[] { (current.FfprobePath, incoming.FfprobePath), (current.MediainfoPath, incoming.MediainfoPath) }
                .Where(p => !PathEquals(p.Item1, p.Item2))
                .Select(p => p.Item2)
                .ToArray();
            if (changed.Length > 0)
            {
                if (principal.Identity?.AuthenticationType == ApiKeyAuthHandler.SchemeName)
                    return Results.Json(new { error = "Sign in with your password to change tool paths." }, statusCode: StatusCodes.Status403Forbidden);

                if (changed.Any(p => !string.IsNullOrWhiteSpace(p) && !File.Exists(p)))
                    return Results.BadRequest(new { error = "That path does not exist or is not a file." });
            }

            await settings.SaveAsync(incoming);
            await tools.RefreshAsync();
            return Results.NoContent();
        });
        return app;
    }

    private static bool PathEquals(string? a, string? b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
}
