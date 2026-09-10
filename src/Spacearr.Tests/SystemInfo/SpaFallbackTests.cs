using System.Net;
using FluentAssertions;

namespace Spacearr.Tests.SystemInfo;

public class SpaFallbackTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public SpaFallbackTests(TestApp app) => _app = app;

    [Fact]
    public async Task Root_serves_index_without_authentication()
    {
        var client = _app.CreateClient();
        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Deep_links_serve_index_and_unknown_api_paths_are_json_404()
    {
        var client = _app.CreateClient();
        var page = await client.GetAsync("/library/anything");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var api = await client.GetAsync("/api/v1/nope");
        api.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }
}
