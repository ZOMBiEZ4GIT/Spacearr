using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spacearr.Arr;

internal sealed class ArrHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public ArrHttp(HttpClient http, string apiKey) { _http = http; _apiKey = apiKey; }

    public async Task<JsonNode> GetAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
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
        using var _ = await SendAsync(method, path, body, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.Add("X-Api-Key", _apiKey);
        request.Headers.Accept.ParseAdd("application/json");
        if (body is not null) request.Content = new StringContent(body.ToJsonString(Json), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, ct); }
        catch (HttpRequestException ex) { throw new ArrException(null, $"Could not reach {_http.BaseAddress}: {ex.Message}. Inside Docker, use the container name rather than localhost.", ex); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { throw new ArrException(null, $"Timed out talking to {_http.BaseAddress}", ex); }
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
