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

    // Without this, an intermediary or the browser could cache the shell itself and keep serving
    // it - and the hashed asset filenames it references - after an upgraded container ships a new
    // build under the same URL.
    [Fact]
    public async Task Fallback_shell_is_served_with_no_cache()
    {
        var client = _app.CreateClient();
        var response = await client.GetAsync("/library/anything");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    // Root_serves_index_without_authentication above doesn't actually prove the
    // pipeline order: "/" also matches the AllowAnonymous SPA fallback's
    // "{*path:nonfile}" pattern (no dot in the last segment), so it would still
    // return 200 text/html even if UseStaticFiles() were moved below
    // UseAuthentication()/UseAuthorization() - the fallback would serve it instead
    // of static files, masking the regression. "/index.html" has a dot in its last
    // segment, so it never matches the nonfile fallback pattern at all; the only way
    // it can succeed anonymously is if the static-file middleware really does run
    // before the auth gate. The placeholder wwwroot/index.html the csproj writes
    // when the web app hasn't been built makes this assertion true even without
    // Node/npm having run.
    [Fact]
    public async Task Index_html_is_served_by_static_files_without_authentication()
    {
        var client = _app.CreateClient();
        var response = await client.GetAsync("/index.html");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task Deep_links_serve_index_html()
    {
        var client = _app.CreateClient();
        var page = await client.GetAsync("/library/anything");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    // Nothing under /api may answer an anonymous caller with anything but 401 - the
    // SPA fallback being AllowAnonymous must not weaken that invariant for a
    // genuinely-unmatched /api path.
    [Fact]
    public async Task Unknown_api_path_is_401_for_anonymous_callers()
    {
        var client = _app.CreateClient();
        var response = await client.GetAsync("/api/v1/nope");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_api_path_is_json_404_for_authenticated_callers()
    {
        var authed = await AuthedClient.CreateAsync(_app);
        var response = await authed.GetAsync("/api/v1/nope");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }
}
