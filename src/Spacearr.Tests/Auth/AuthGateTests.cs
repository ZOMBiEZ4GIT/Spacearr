using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Spacearr.Tests.Library;

namespace Spacearr.Tests.Auth;

public partial class AuthGateTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AuthGateTests(TestApp app) => _app = app;

    // Replaces {id}, {id:int}, {*rest} and friends with a concrete "1" so a
    // parameterised pattern turns into a path the router can actually match.
    [GeneratedRegex(@"\{\*?(\w+)(:[^}]+)?\}")]
    private static partial Regex RouteParameter();

    [Fact]
    public async Task Every_api_route_except_status_rejects_anonymous_requests()
    {
        var authed = await AuthedClient.CreateAsync(_app);
        // Gives the id-1 routes (library item, instance, ...) something real to find,
        // so a 404 from them means "no such route" rather than "empty database".
        await Seed.LibraryAsync(_app, movies: 1);

        var anon = _app.CreateClient();
        var sources = _app.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var routes = sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/"))
            .Select(e => (Pattern: e.RoutePattern.RawText!, Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" }))
            .ToList();
        routes.Should().NotBeEmpty();
        routes.Should().Contain(r => r.Pattern.Contains("openapi.json"), "the OpenAPI document lists every route, so it must be swept like the rest of the API");

        var failures = new List<string>();
        foreach (var (pattern, methods) in routes)
        {
            if (pattern == "/api/v1/system/status" || pattern == "/api/v1/setup" || pattern == "/api/v1/auth/login") continue;
            // {documentName} is the OpenAPI document name, not an id: only "v1" exists.
            var path = RouteParameter().Replace(pattern.Replace("{documentName}", "v1"), "1");
            foreach (var method in methods)
            {
                var response = await anon.SendAsync(NewRequest(method, path));
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                {
                    failures.Add($"anonymous {method} {path} -> {(int)response.StatusCode}");
                    continue;
                }

                // The 401 above only proves the gate fires; it would fire just the same
                // for a path that matches no route at all (an unauthenticated request to
                // a non-existent path is rejected before routing can 404 it). So also
                // check the substituted path really reaches a handler when authenticated:
                // any status but 404 proves it did. Paths ending in an id segment are
                // exempt, because their handler legitimately 404s for id 1 - for those the
                // proof of the auth gate above is all we need.
                var authedResponse = await authed.SendAsync(NewRequest(method, path));
                if (authedResponse.StatusCode == HttpStatusCode.NotFound && !path.EndsWith("/1", StringComparison.Ordinal))
                    failures.Add($"authenticated {method} {path} -> 404 (does the substituted path match any route?)");
            }
        }
        failures.Should().BeEmpty("every API route must require authentication and must match the path the sweep builds for it");
    }

    // A bodyless POST/PUT/PATCH won't match a minimal-API endpoint that declares a JSON
    // body parameter (ASP.NET Core's content-type matcher policy eliminates it as a
    // candidate), and since the SPA fallback now matches every otherwise-unmatched path,
    // such a request falls through to the anonymous fallback instead of the real,
    // auth-gated endpoint. Sending a JSON body - as any real client would - keeps this
    // sweep testing the actual route instead of tripping over that routing nuance.
    private static HttpRequestMessage NewRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH") request.Content = JsonContent.Create(new { });
        return request;
    }
}
