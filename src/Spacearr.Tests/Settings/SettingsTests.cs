using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Settings;

public class SettingsTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public SettingsTests(TestApp app) => _app = app;

    [Fact]
    public async Task Defaults_then_update_round_trip()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var defaults = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        defaults!.ScanIntervalHours.Should().Be(6);
        defaults.Extensions.Should().Contain(".mkv");
        defaults.HeatMode.Should().Be("relative");

        var put = await client.PutAsJsonAsync("/api/v1/settings", defaults with { ScanIntervalHours = 12, HeatMode = "absolute" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updated = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        updated!.ScanIntervalHours.Should().Be(12);
        updated.HeatMode.Should().Be("absolute");
    }

    [Fact]
    public async Task Rejects_invalid_values()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var current = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        var bad = await client.PutAsJsonAsync("/api/v1/settings", current! with { ScanIntervalHours = 999 });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var badExt = await client.PutAsJsonAsync("/api/v1/settings", current with { Extensions = new[] { "mkv" } });
        badExt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tool_path_change_requires_a_cookie_session()
    {
        using var fresh = new TestApp();
        var cookieClient = await AuthedClient.CreateAsync(fresh);
        var me = await cookieClient.GetFromJsonAsync<MeDto>("/api/v1/auth/me");
        var current = await cookieClient.GetFromJsonAsync<SettingsDto>("/api/v1/settings");

        var keyClient = fresh.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", me!.ApiKey);

        var toolFile = Path.Combine(fresh.ConfigDir, "ffprobe-stub.exe");
        await File.WriteAllTextAsync(toolFile, "stub");

        var forbidden = await keyClient.PutAsJsonAsync("/api/v1/settings", current! with { FfprobePath = toolFile });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forbidden.Content.ReadAsStringAsync()).Should().Contain("Sign in with your password");

        // A non-tool-path change over the same API key is still allowed.
        var allowed = await keyClient.PutAsJsonAsync("/api/v1/settings", current with { ScanIntervalHours = 9 });
        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The same change over a cookie session succeeds.
        var ok = await cookieClient.PutAsJsonAsync("/api/v1/settings", current with { ScanIntervalHours = 9, FfprobePath = toolFile });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await cookieClient.GetFromJsonAsync<SettingsDto>("/api/v1/settings");
        after!.FfprobePath.Should().Be(toolFile);
    }

    [Fact]
    public async Task Tool_path_must_exist_as_a_file()
    {
        using var fresh = new TestApp();
        var client = await AuthedClient.CreateAsync(fresh);
        var current = await client.GetFromJsonAsync<SettingsDto>("/api/v1/settings");

        var missing = await client.PutAsJsonAsync("/api/v1/settings",
            current! with { MediainfoPath = Path.Combine(fresh.ConfigDir, "no-such-tool") });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.Content.ReadAsStringAsync()).Should().Contain("does not exist or is not a file");

        // A directory is not a file either.
        var directory = await client.PutAsJsonAsync("/api/v1/settings", current with { MediainfoPath = fresh.ConfigDir });
        directory.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Clearing a tool path back to empty is always allowed.
        var cleared = await client.PutAsJsonAsync("/api/v1/settings", current with { MediainfoPath = null });
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record MeDto(string Username, string ApiKey);
    private sealed record SettingsDto(int ScanIntervalHours, string[] Extensions, string? FfprobePath, string? MediainfoPath, string HeatMode, string Theme);
}
