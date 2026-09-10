using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Spacearr.Tests.Arr;

public class InstanceEndpointTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public InstanceEndpointTests(ArrTestApp app) => _app = app;

    [Fact]
    public async Task Test_endpoint_reports_ok_with_roots_and_profiles_and_bad_key_as_error()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var ok = await (await client.PostAsJsonAsync("/api/v1/instances/test", new { type = "radarr", baseUrl = "http://radarr:7878", apiKey = "secret" })).Content.ReadFromJsonAsync<TestDto>();
        ok!.Ok.Should().BeTrue();
        ok.Version.Should().StartWith("6.");
        ok.RootFolders.Should().Equal("/data/movies");
        ok.Profiles.Should().Contain(p => p.Name == "Ultra-HD");

        var bad = await (await client.PostAsJsonAsync("/api/v1/instances/test", new { type = "radarr", baseUrl = "http://radarr:7878", apiKey = "nope" })).Content.ReadFromJsonAsync<TestDto>();
        bad!.Ok.Should().BeFalse();
        bad.Error.Should().Contain("API key");
    }

    [Fact]
    public async Task Crud_never_returns_the_key_and_put_keeps_key_when_blank()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var created = await client.PostAsJsonAsync("/api/v1/instances", new { type = "radarr", name = "Movies", baseUrl = "http://radarr:7878", apiKey = "secret" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var raw = await created.Content.ReadAsStringAsync();
        raw.Should().NotContain("secret");
        var inst = await created.Content.ReadFromJsonAsync<InstanceDto>();
        inst!.ApiKeySet.Should().BeTrue();

        var put = await client.PutAsJsonAsync($"/api/v1/instances/{inst.Id}", new { type = "radarr", name = "Movies 4K", baseUrl = "http://radarr:7878", enabled = true, apiKey = "" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillWorks = await (await client.PostAsync($"/api/v1/instances/{inst.Id}/test", null)).Content.ReadFromJsonAsync<TestDto>();
        stillWorks!.Ok.Should().BeTrue("the stored key must survive a PUT with a blank apiKey");

        var profiles = await client.GetFromJsonAsync<ProfileDto[]>($"/api/v1/instances/{inst.Id}/profiles");
        profiles.Should().Contain(p => p.Id == 5);

        var invalid = await client.PostAsJsonAsync("/api/v1/instances", new { type = "radarr", name = "x", baseUrl = "radarr:7878", apiKey = "k" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.DeleteAsync($"/api/v1/instances/{inst.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Mappings_crud_and_suggest()
    {
        var client = await AuthedClient.CreateAsync(_app);
        var inst = await (await client.PostAsJsonAsync("/api/v1/instances", new { type = "radarr", name = "R", baseUrl = "http://radarr:7878", apiKey = "secret" })).Content.ReadFromJsonAsync<InstanceDto>();

        var local = Path.Combine(Path.GetTempPath(), "spacearr-map-" + Guid.NewGuid().ToString("N"), "movies");
        Directory.CreateDirectory(local);
        await client.PostAsJsonAsync("/api/v1/roots", new { path = local });

        var suggestions = await (await client.PostAsync($"/api/v1/instances/{inst!.Id}/mappings/suggest", null)).Content.ReadFromJsonAsync<SuggestDto[]>();
        suggestions.Should().ContainSingle(s => s.RemotePrefix == "/data/movies" && s.Confidence == "high");
        suggestions![0].LocalPrefix.Should().EndWith("movies");

        var m = await client.PostAsJsonAsync($"/api/v1/instances/{inst.Id}/mappings", new { remotePrefix = "/data/movies", localPrefix = local });
        m.StatusCode.Should().Be(HttpStatusCode.Created);
        var mapping = await m.Content.ReadFromJsonAsync<MappingDto>();

        var list = await client.GetFromJsonAsync<InstanceDto[]>("/api/v1/instances");
        list!.Single(i => i.Id == inst.Id).Mappings.Should().ContainSingle(x => x.Id == mapping!.Id);

        (await client.DeleteAsync($"/api/v1/instances/{inst.Id}/mappings/{mapping!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Directory.Delete(Path.GetDirectoryName(local)!, true);
    }

    private sealed record TestDto(bool Ok, string? Error, string? Version, string? AppName, string[] RootFolders, ProfileDto[] Profiles);
    private sealed record ProfileDto(int Id, string Name);
    private sealed record MappingDto(int Id, string RemotePrefix, string LocalPrefix);
    private sealed record InstanceDto(int Id, string Type, string Name, string BaseUrl, bool Enabled, bool ApiKeySet, MappingDto[] Mappings, int Matched, int Unmatched);
    private sealed record SuggestDto(string RemotePrefix, string LocalPrefix, string Confidence);
}
