using System.Net;
using System.Text;
using System.Text.Json;

namespace Spacearr.Tests.Arr;

public sealed class FakeArrHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, (HttpStatusCode, string)>> _routes = new();
    public List<(HttpMethod Method, string Path, string? Body)> Calls { get; } = new();
    public string? RequiredApiKey { get; set; } = "secret";

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>Raw JSON of the first element of an array fixture, for single-object routes like /movie/{id}.</summary>
    public static string First(string name) => JsonDocument.Parse(Fixture(name)).RootElement[0].GetRawText();

    public FakeArrHandler Map(string method, string pathAndQuery, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[$"{method} {pathAndQuery}"] = _ => (status, json);
        return this;
    }

    public FakeArrHandler MapFixture(string method, string pathAndQuery, string fixture) => Map(method, pathAndQuery, Fixture(fixture));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        var path = request.RequestUri!.PathAndQuery;
        Calls.Add((request.Method, path, body));
        var provided = request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.FirstOrDefault() : null;
        if (RequiredApiKey is not null && provided != RequiredApiKey)
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        if (_routes.TryGetValue($"{request.Method.Method} {path}", out var handler))
        {
            var (status, json) = handler(request);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"no route for {request.Method} {path}") };
    }
}
