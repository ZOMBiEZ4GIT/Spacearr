using System.Net;
using System.Net.Http.Json;

namespace Spacearr.Tests;

public static class AuthedClient
{
    public const string Username = "admin";
    public const string Password = "correct horse battery staple";

    public static async Task<HttpClient> CreateAsync(TestApp app)
    {
        var client = app.CreateClient(new() { HandleCookies = true });
        var setup = await client.PostAsJsonAsync("/api/v1/setup", new { username = Username, password = Password });
        if (setup.StatusCode != HttpStatusCode.Created && setup.StatusCode != HttpStatusCode.Conflict)
            throw new InvalidOperationException($"setup failed: {setup.StatusCode}");
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = Username, password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
