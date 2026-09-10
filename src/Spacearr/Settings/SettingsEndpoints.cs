namespace Spacearr.Settings;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings").RequireAuthorization();
        group.MapGet("/", async (ISettingsService settings) => Results.Ok(await settings.GetAsync()));
        group.MapPut("/", async (AppSettings incoming, ISettingsService settings) =>
        {
            var error = incoming.Validate();
            if (error is not null) return Results.BadRequest(new { error });
            await settings.SaveAsync(incoming);
            return Results.NoContent();
        });
        return app;
    }
}
