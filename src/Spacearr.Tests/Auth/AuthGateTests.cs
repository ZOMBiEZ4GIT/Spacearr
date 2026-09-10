using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Spacearr.Tests.Auth;

public class AuthGateTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AuthGateTests(TestApp app) => _app = app;

    [Fact]
    public async Task Every_api_route_except_status_rejects_anonymous_requests()
    {
        await AuthedClient.CreateAsync(_app);
        var anon = _app.CreateClient();
        var sources = _app.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var routes = sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/"))
            .Select(e => (Pattern: e.RoutePattern.RawText!, Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" }))
            .ToList();
        routes.Should().NotBeEmpty();

        var failures = new List<string>();
        foreach (var (pattern, methods) in routes)
        {
            if (pattern == "/api/v1/system/status" || pattern == "/api/v1/setup" || pattern == "/api/v1/auth/login") continue;
            var path = pattern.Replace("{id}", "1").Replace("{itemId}", "1").Replace("{jobId}", "1").Replace("{instanceId}", "1").Replace("{mappingId}", "1");
            foreach (var method in methods)
            {
                var response = await anon.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                    failures.Add($"{method} {path} -> {(int)response.StatusCode}");
            }
        }
        failures.Should().BeEmpty("every API route must require authentication");
    }
}
