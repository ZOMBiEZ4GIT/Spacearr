using Serilog;
using Spacearr.Infrastructure;
using Spacearr.System;

var builder = WebApplication.CreateBuilder(args);

var configRoot = ConfigPaths.Resolve(builder.Configuration);
var paths = new ConfigPaths(configRoot);
builder.Services.AddSingleton(paths);

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
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/openapi.json");
app.MapSystemEndpoints();

app.Run();

public partial class Program { }
