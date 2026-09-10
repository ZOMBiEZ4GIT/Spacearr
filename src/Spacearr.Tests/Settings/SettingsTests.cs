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

    private sealed record SettingsDto(int ScanIntervalHours, string[] Extensions, string? FfprobePath, string? MediainfoPath, string HeatMode, string Theme);
}
