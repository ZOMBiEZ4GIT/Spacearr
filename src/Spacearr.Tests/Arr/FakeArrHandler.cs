using System.Net;
using System.Text;
using System.Text.Json;

namespace Spacearr.Tests.Arr;

public sealed class FakeArrHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, (HttpStatusCode Status, string Json, string ContentType)>> _routes = new();
    public List<(HttpMethod Method, string Path, string? Body)> Calls { get; } = new();
    public string? RequiredApiKey { get; set; } = "secret";

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>Raw JSON of the first element of an array fixture, for single-object routes like /movie/{id}.</summary>
    public static string First(string name) => JsonDocument.Parse(Fixture(name)).RootElement[0].GetRawText();

    /// <param name="host">
    /// When given, the route answers only requests to that host, so one handler can
    /// serve two arr instances differently (e.g. one healthy, one returning junk).
    /// Routes without a host answer any host that has no more specific route.
    /// </param>
    public FakeArrHandler Map(string method, string pathAndQuery, string json, HttpStatusCode status = HttpStatusCode.OK, string contentType = "application/json", string? host = null)
    {
        _routes[Key(method, host, pathAndQuery)] = _ => (status, json, contentType);
        return this;
    }

    public FakeArrHandler MapFixture(string method, string pathAndQuery, string fixture) => Map(method, pathAndQuery, Fixture(fixture));

    private static string Key(string method, string? host, string pathAndQuery) => $"{method} {host ?? "*"} {pathAndQuery}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        var path = request.RequestUri!.PathAndQuery;
        Calls.Add((request.Method, path, body));
        var provided = request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.FirstOrDefault() : null;
        if (RequiredApiKey is not null && provided != RequiredApiKey)
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        if (_routes.TryGetValue(Key(request.Method.Method, request.RequestUri.Host, path), out var handler) ||
            _routes.TryGetValue(Key(request.Method.Method, null, path), out handler))
        {
            var (status, json, contentType) = handler(request);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, contentType) };
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"no route for {request.Method} {path}") };
    }
}
