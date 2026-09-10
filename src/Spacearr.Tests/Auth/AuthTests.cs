using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Auth;

public class AuthTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AuthTests(TestApp app) => _app = app;

    [Fact]
    public async Task Setup_then_login_then_me_and_setup_is_one_shot()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var me = await client.GetFromJsonAsync<MeDto>("/api/v1/auth/me");
        me!.Username.Should().Be(AuthedClient.Username);
        me.ApiKey.Should().HaveLength(32);

        var again = await _app.CreateClient().PostAsJsonAsync("/api/v1/setup", new { username = "x", password = "yyyyyyyyyyyy" });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var status = await _app.CreateClient().GetFromJsonAsync<StatusDto>("/api/v1/system/status");
        status!.SetupComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Api_key_header_authenticates()
    {
        var cookieClient = await AuthedClient.CreateAsync(_app);
        var me = await cookieClient.GetFromJsonAsync<MeDto>("/api/v1/auth/me");
        var keyClient = _app.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", me!.ApiKey);
        var response = await keyClient.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_is_401_and_sixth_attempt_is_429()
    {
        // Uses its own TestApp: this test intentionally locks out the shared
        // admin username, which would otherwise leak into sibling tests that
        // share the class fixture and log in as the same user.
        using var fresh = new TestApp();
        await AuthedClient.CreateAsync(fresh);
        var client = fresh.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"203.0.113.{i}");
            var r = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "wrong" });
            r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.99");
        var sixth = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "wrong" });
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        sixth.Headers.RetryAfter!.ToString().Should().Be("60");
    }

    [Fact]
    public async Task Setup_rejects_short_passwords()
    {
        using var fresh = new TestApp();
        var r = await fresh.CreateClient().PostAsJsonAsync("/api/v1/setup", new { username = "a", password = "short" });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Api_key_caller_cannot_change_password_or_regenerate_key()
    {
        var cookieClient = await AuthedClient.CreateAsync(_app);
        var me = await cookieClient.GetFromJsonAsync<MeDto>("/api/v1/auth/me");
        var keyClient = _app.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", me!.ApiKey);

        var passwordResponse = await keyClient.PostAsJsonAsync("/api/v1/auth/password",
            new { currentPassword = AuthedClient.Password, newPassword = "a-new-long-enough-password" });
        passwordResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var regenResponse = await keyClient.PostAsync("/api/v1/auth/apikey/regenerate", content: null);
        regenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Password_change_requires_current_password()
    {
        using var fresh = new TestApp();
        var client = await AuthedClient.CreateAsync(fresh);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "wrong", newPassword = "a-new-long-enough-password" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var right = await client.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = AuthedClient.Password, newPassword = "a-new-long-enough-password" });
        right.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await fresh.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "a-new-long-enough-password" });
        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record MeDto(string Username, string ApiKey);
    private sealed record StatusDto(string Version, bool SetupComplete);
}
