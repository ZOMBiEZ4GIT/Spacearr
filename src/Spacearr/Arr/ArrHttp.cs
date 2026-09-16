using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spacearr.Arr;

/// <summary>
/// How long a single arr request may take. <paramref name="Request"/> covers the
/// interactive calls - a connection test, a profile list, an action - where a hung arr
/// should fail fast. <paramref name="Bulk"/> covers the whole-library listings a sync
/// makes, which are legitimately slow on a large library: right after a scan of ~40k
/// files, Sonarr's /api/v3/series took 67-92 s against 0.2 s idle, because probing had
/// evicted its SQLite pages from the page cache. At 30 s flat, every first sync on a
/// large library failed and the user's TV library stayed unmatched until the next scan.
/// </summary>
public sealed record ArrTimeouts(TimeSpan Request, TimeSpan Bulk)
{
    public static readonly ArrTimeouts Default = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
}

internal sealed class ArrHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ArrTimeouts _timeouts;

    public ArrHttp(HttpClient http, string apiKey, ArrTimeouts? timeouts = null) { _http = http; _apiKey = apiKey; _timeouts = timeouts ?? ArrTimeouts.Default; }

    /// <summary>A whole-library listing, on the longer <see cref="ArrTimeouts.Bulk"/> budget.</summary>
    public Task<JsonNode> GetBulkAsync(string path, CancellationToken ct) => GetAsync(path, ct, _timeouts.Bulk);

    public Task<JsonNode> GetAsync(string path, CancellationToken ct) => GetAsync(path, ct, _timeouts.Request);

    private async Task<JsonNode> GetAsync(string path, CancellationToken ct, TimeSpan timeout)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct, timeout);
        var text = await response.Content.ReadAsStringAsync(ct);
        JsonNode? node;
        // A 200 that isn't JSON at all (a login page, a reverse proxy's HTML error,
        // the wrong app entirely) must surface as an ArrException the endpoints
        // already know how to report, never as an unhandled JsonException -> 500.
        try { node = JsonNode.Parse(text); }
        catch (JsonException ex) { throw new ArrException(null, $"{_http.BaseAddress} did not return JSON. Is this really a Radarr/Sonarr URL?", ex); }
        return node ?? throw new ArrException(null, $"Empty response from {path}");
    }

    /// <summary>
    /// Asserts that a response we expect to be a list really is one. JsonNode.AsArray()
    /// throws InvalidOperationException on an object/scalar, which would escape as a 500.
    /// </summary>
    internal static JsonArray Array(JsonNode node, string what) =>
        node as JsonArray ?? throw new ArrException(null, $"Unexpected response shape from {what}: expected a list.");

    public async Task SendJsonAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        using var _ = await SendAsync(method, path, body, ct, _timeouts.Request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct, TimeSpan timeout)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.Add("X-Api-Key", _apiKey);
        request.Headers.Accept.ParseAdd("application/json");
        if (body is not null) request.Content = new StringContent(body.ToJsonString(Json), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        // The timeout is per request, not on the HttpClient: a sync's whole-library
        // listings get a far longer budget than the interactive calls (see ArrTimeouts).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try { response = await _http.SendAsync(request, cts.Token); }
        catch (HttpRequestException ex) { throw new ArrException(null, $"Could not reach {_http.BaseAddress}: {ex.Message}. Inside Docker, use the container name rather than localhost.", ex); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } // caller cancelled (job stopped): propagate as-is
        catch (TaskCanceledException ex) { throw new ArrException(null, $"Timed out talking to {_http.BaseAddress} after {timeout.TotalSeconds:0} s", ex); }
        if (response.StatusCode == HttpStatusCode.Unauthorized) { response.Dispose(); throw new ArrException(HttpStatusCode.Unauthorized, "Unauthorized: check the API key"); }
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();
            throw new ArrException(response.StatusCode, $"{(int)response.StatusCode} from {path}: {Truncate(text)}");
        }
        return response;
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "…";

    public static int? Int(JsonNode? n) => n is null ? null : n.GetValueKind() == JsonValueKind.Number ? n.GetValue<int>() : int.TryParse(n.ToString(), out var i) ? i : null;
    public static long? Long(JsonNode? n) => n is null ? null : n.GetValueKind() == JsonValueKind.Number ? n.GetValue<long>() : long.TryParse(n.ToString(), out var l) ? l : null;
    public static string? Str(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : null;
    public static bool Bool(JsonNode? n) => n?.GetValueKind() == JsonValueKind.True;
    public static int[] Ints(JsonNode? n) => n is JsonArray a ? a.Select(x => Int(x) ?? 0).ToArray() : System.Array.Empty<int>();
    public static string? Poster(JsonNode? images) => images is JsonArray a
        ? a.Select(i => i!.AsObject()).Where(i => Str(i["coverType"]) == "poster").Select(i => Str(i["url"]) ?? Str(i["remoteUrl"])).FirstOrDefault(u => !string.IsNullOrEmpty(u))
        : null;
}
