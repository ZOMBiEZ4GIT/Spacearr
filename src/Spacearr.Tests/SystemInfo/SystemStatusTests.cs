using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.SystemInfo;

public class SystemStatusTests : IClassFixture<TestApp>
{
    private readonly HttpClient _client;
    public SystemStatusTests(TestApp app) => _client = app.CreateClient();

    [Fact]
    public async Task Status_is_public_and_reports_setup_incomplete_on_fresh_install()
    {
        var response = await _client.GetAsync("/api/v1/system/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StatusDto>();
        body!.Version.Should().NotBeNullOrWhiteSpace();
        body.SetupComplete.Should().BeFalse();
    }

    private sealed record StatusDto(string Version, bool SetupComplete);
}
