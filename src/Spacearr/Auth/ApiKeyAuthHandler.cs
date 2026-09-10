using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Spacearr.Auth;

public sealed class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private readonly IUserService _users;

    public ApiKeyAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IUserService users)
        : base(options, logger, encoder) => _users = users;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? key = Request.Headers["X-Api-Key"].FirstOrDefault() ?? Request.Query["apikey"].FirstOrDefault();
        if (string.IsNullOrEmpty(key)) return AuthenticateResult.NoResult();
        var user = await _users.FindByApiKeyAsync(key);
        if (user is null) return AuthenticateResult.Fail("Invalid API key");
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        }, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
