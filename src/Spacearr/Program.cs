using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Spacearr.Actions;
using Spacearr.Arr;
using Spacearr.Auth;
using Spacearr.Data;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Library;
using Spacearr.Scanning;
using Spacearr.Settings;
using Spacearr.SystemInfo;

var builder = WebApplication.CreateBuilder(args);

var configRoot = ConfigPaths.Resolve(builder.Configuration);
var paths = new ConfigPaths(configRoot);
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();
builder.Services.AddDbContext<SpacearrDb>(o =>
    o.UseSqlite($"Data Source={paths.DatabasePath};Default Timeout=30"));

builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(paths.LogDirectory, "spacearr-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7));

var port = builder.Configuration["SPACEARR_PORT"] ?? "8787";
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSpacearrAuth();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSpacearrJobs();
builder.Services.AddSpacearrScanning();
builder.Services.AddSpacearrArr();
builder.Services.AddSpacearrActions();

var app = builder.Build();

await StartupTasks.MigrateAndGuardAsync(app.Services, typeof(Program).Assembly.GetName().Version ?? new Version(0, 0, 0));
await app.Services.GetRequiredService<IToolLocator>().RefreshAsync();

// Every error this API returns - validation, conflict, or crash - uses the
// same JSON shape: { "error": "<message>" }. Unhandled exceptions are logged
// in full and reduced to a generic message, so no stack trace or internal
// detail ever reaches the client.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Spacearr.UnhandledException");
    logger.LogError(feature?.Error, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { error = "Something went wrong. Check the Spacearr log." });
}));

app.UseSerilogRequestLogging();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
// The OpenAPI document describes every route in the app, so it sits behind the same
// auth gate as the API itself (the endpoint-based form is what makes RequireAuthorization
// possible - the UseSwagger middleware form runs before routing and cannot be gated).
app.MapSwagger("/api/docs/{documentName}/openapi.json").RequireAuthorization();
app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapSettingsEndpoints();
app.MapJobEndpoints();
app.MapRootFolderEndpoints();
app.MapInstanceEndpoints();
app.MapLibraryEndpoints();
app.MapActionEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    // Exists only so the test suite can exercise the exception handler above
    // against a real unhandled exception on a real authenticated route.
    app.MapGet("/api/v1/test/throw", IResult () => throw new InvalidOperationException("boom")).RequireAuthorization();
}

// Serves the React SPA for any route that isn't an API endpoint (so deep
// links like /library/123 resolve to index.html and client-side routing can
// take over), and a JSON 404 for unmatched /api paths instead of HTML. This
// endpoint is AllowAnonymous so it can serve the SPA shell to a logged-out
// visitor, but that must never leak into the API's auth contract: nothing
// under /api may answer an anonymous caller with anything but 401, so an
// unmatched /api path checks authentication itself before deciding between
// a bare 401 (anonymous - matches every other API route) and a JSON 404
// (authenticated - a real "no such route").
app.MapFallback(async ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        if (ctx.User?.Identity?.IsAuthenticated != true) { ctx.Response.StatusCode = 401; return; }
        ctx.Response.StatusCode = 404;
        await ctx.Response.WriteAsJsonAsync(new { error = "Not found" });
        return;
    }
    ctx.Response.ContentType = "text/html";
    await ctx.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "index.html"));
}).AllowAnonymous();

app.Run();

public partial class Program { }
