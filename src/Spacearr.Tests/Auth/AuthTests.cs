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
        await AuthedClient.CreateAsync(_app);
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.9");
        for (var i = 0; i < 5; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "wrong" });
            r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        var sixth = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = AuthedClient.Username, password = "wrong" });
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Setup_rejects_short_passwords()
    {
        using var fresh = new TestApp();
        var r = await fresh.CreateClient().PostAsJsonAsync("/api/v1/setup", new { username = "a", password = "short" });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record MeDto(string Username, string ApiKey);
    private sealed record StatusDto(string Version, bool SetupComplete);
}
