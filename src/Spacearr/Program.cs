using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Spacearr.Auth;
using Spacearr.Data;
using Spacearr.Infrastructure;
using Spacearr.Jobs;
using Spacearr.Settings;
using Spacearr.System;

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

var app = builder.Build();

await StartupTasks.MigrateAndGuardAsync(app.Services, typeof(Program).Assembly.GetName().Version ?? new Version(0, 0, 0));

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
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/openapi.json");
app.MapSystemEndpoints();
app.MapAuthEndpoints();
app.MapSettingsEndpoints();
app.MapJobEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    // Exists only so the test suite can exercise the exception handler above
    // against a real unhandled exception on a real authenticated route.
    app.MapGet("/api/v1/test/throw", IResult () => throw new InvalidOperationException("boom")).RequireAuthorization();
}

app.Run();

public partial class Program { }
