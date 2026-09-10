using Microsoft.EntityFrameworkCore;
using Serilog;
using Spacearr.Auth;
using Spacearr.Data;
using Spacearr.Infrastructure;
using Spacearr.System;

var builder = WebApplication.CreateBuilder(args);

var configRoot = ConfigPaths.Resolve(builder.Configuration);
var paths = new ConfigPaths(configRoot);
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<Spacearr.Infrastructure.ISecretProtector, Spacearr.Infrastructure.SecretProtector>();
builder.Services.AddDbContext<SpacearrDb>(o =>
    o.UseSqlite($"Data Source={paths.DatabasePath};Cache=Shared"));

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

builder.Services.AddScoped<Spacearr.Auth.IUserService, Spacearr.Auth.UserService>();
builder.Services.AddSingleton<Spacearr.Auth.LoginThrottle>();
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "Spacearr.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Spacearr.Auth.ApiKeyAuthHandler>(Spacearr.Auth.ApiKeyAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(o =>
{
    o.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            Spacearr.Auth.ApiKeyAuthHandler.SchemeName)
        .RequireAuthenticatedUser().Build();
    o.FallbackPolicy = o.DefaultPolicy;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SpacearrDb>();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.MigrateAsync();
    var appVersion = typeof(Program).Assembly.GetName().Version ?? new Version(0, 0, 0);
    var stored = await db.Settings.FindAsync("schema.appVersion");
    if (stored is not null && Version.TryParse(stored.Value, out var storedVersion) && storedVersion > appVersion)
    {
        throw new InvalidOperationException(
            $"Database was created by Spacearr {storedVersion}, newer than this build {appVersion}. Refusing to start.");
    }
    if (stored is null) db.Settings.Add(new Spacearr.Data.Entities.Setting { Key = "schema.appVersion", Value = appVersion.ToString(3) });
    else stored.Value = appVersion.ToString(3);
    await db.SaveChangesAsync();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/openapi.json");
app.MapSystemEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { }
