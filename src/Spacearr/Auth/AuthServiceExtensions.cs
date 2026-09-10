using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

namespace Spacearr.Auth;

public static class AuthServiceExtensions
{
    /// <summary>
    /// Persists ASP.NET Core's Data Protection keyring under the config
    /// directory instead of the container's ephemeral filesystem, so the
    /// auth cookie (and anything else protected with it) stays valid across
    /// a container recreate or restart - without this, every restart mints
    /// a fresh key and silently logs everyone out.
    /// </summary>
    public static IServiceCollection AddSpacearrDataProtection(this IServiceCollection services, string configDir)
    {
        var keysDirectory = new DirectoryInfo(Path.Combine(configDir, "keys"));
        keysDirectory.Create();
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keysDirectory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        services.AddDataProtection()
            .PersistKeysToFileSystem(keysDirectory)
            .SetApplicationName("Spacearr");
        return services;
    }

    /// <summary>
    /// Registers the user store, the login throttle, both authentication
    /// schemes (session cookie and API key) and the fallback policy that
    /// makes every endpoint authenticated unless it opts out.
    /// </summary>
    public static IServiceCollection AddSpacearrAuth(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<LoginThrottle>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, _ => { });

        services.AddAuthorization(o =>
        {
            o.DefaultPolicy = new AuthorizationPolicyBuilder(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    ApiKeyAuthHandler.SchemeName)
                .RequireAuthenticatedUser().Build();
            o.FallbackPolicy = o.DefaultPolicy;
        });

        return services;
    }
}
