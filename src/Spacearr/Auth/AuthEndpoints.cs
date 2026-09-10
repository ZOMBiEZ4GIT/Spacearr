using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Spacearr.Auth;

public sealed record SetupRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record MeResponse(string Username, string ApiKey);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/setup", async (SetupRequest req, IUserService users) =>
        {
            if (await users.IsSetupCompleteAsync()) return Results.Conflict(new { error = "Setup is already complete." });
            if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length > 64) return Results.BadRequest(new { error = "Username is required (max 64 characters)." });
            if (req.Password is null || req.Password.Length < 10) return Results.BadRequest(new { error = "Password must be at least 10 characters." });
            var user = await users.CreateAdminAsync(req.Username, req.Password);
            return Results.Created("/api/v1/auth/me", new MeResponse(user.Username, user.ApiKey));
        }).AllowAnonymous();

        app.MapPost("/api/v1/auth/login", async (LoginRequest req, HttpContext http, IUserService users, LoginThrottle throttle) =>
        {
            var userKey = $"user:{(req.Username ?? "").Trim().ToLowerInvariant()}";
            var ipKey = $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            if (throttle.IsLocked(userKey) || throttle.IsLocked(ipKey))
            {
                http.Response.Headers.RetryAfter = "60";
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }
            var user = await users.ValidateAsync(req.Username ?? "", req.Password ?? "");
            if (user is null)
            {
                throttle.RecordFailure(userKey);
                throttle.RecordFailure(ipKey);
                return Results.Unauthorized();
            }
            throttle.Reset(userKey);
            throttle.Reset(ipKey);
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
            return Results.NoContent();
        }).AllowAnonymous();

        var group = app.MapGroup("/api/v1/auth").RequireAuthorization();

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, IUserService users) =>
        {
            var user = await users.FindByIdAsync(UserId(principal));
            return user is null ? Results.Unauthorized() : Results.Ok(new MeResponse(user.Username, user.ApiKey));
        });

        group.MapPost("/apikey/regenerate", async (ClaimsPrincipal principal, IUserService users) =>
        {
            if (IsApiKeyCaller(principal)) return CookieOnly();
            return Results.Ok(new { apiKey = await users.RegenerateApiKeyAsync(UserId(principal)) });
        });

        group.MapPost("/password", async (ChangePasswordRequest req, ClaimsPrincipal principal, IUserService users) =>
        {
            if (IsApiKeyCaller(principal)) return CookieOnly();
            if (req.NewPassword is null || req.NewPassword.Length < 10) return Results.BadRequest(new { error = "Password must be at least 10 characters." });
            var user = await users.FindByIdAsync(UserId(principal));
            if (user is null) return Results.Unauthorized();
            var verified = await users.ValidateAsync(user.Username, req.CurrentPassword ?? "");
            if (verified is null) return Results.Json(new { error = "Current password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
            await users.ChangePasswordAsync(UserId(principal), req.NewPassword);
            return Results.NoContent();
        });

        return app;
    }

    public static int UserId(ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static bool IsApiKeyCaller(ClaimsPrincipal principal) =>
        principal.Identity?.AuthenticationType == ApiKeyAuthHandler.SchemeName;

    private static IResult CookieOnly() =>
        Results.Json(new { error = "Sign in with your password to change account settings." }, statusCode: StatusCodes.Status403Forbidden);
}
