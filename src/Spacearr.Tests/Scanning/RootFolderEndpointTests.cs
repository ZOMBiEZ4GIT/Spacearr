using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public class RootFolderEndpointTests : IClassFixture<TestApp>, IDisposable
{
    private readonly TestApp _app;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spacearr-root-" + Guid.NewGuid().ToString("N"));
    public RootFolderEndpointTests(TestApp app) { _app = app; Directory.CreateDirectory(_dir); }
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public async Task Crud_and_validate()
    {
        var client = await AuthedClient.CreateAsync(_app);
        File.WriteAllBytes(Path.Combine(_dir, "x.mkv"), new byte[2_000_000]);

        var relative = await client.PostAsJsonAsync("/api/v1/roots", new { path = ".." });
        relative.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var validate = await client.PostAsJsonAsync("/api/v1/roots/validate", new { path = _dir });
        var v = await validate.Content.ReadFromJsonAsync<ValidateDto>();
        v!.Exists.Should().BeTrue();
        v.SampleFiles.Should().ContainSingle(s => s.EndsWith("x.mkv"));

        var missing = await client.PostAsJsonAsync("/api/v1/roots", new { path = Path.Combine(_dir, "nope") });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await client.PostAsJsonAsync("/api/v1/roots", new { path = _dir });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var root = await created.Content.ReadFromJsonAsync<RootDto>();

        var dup = await client.PostAsJsonAsync("/api/v1/roots", new { path = _dir + Path.DirectorySeparatorChar });
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var list = await client.GetFromJsonAsync<RootDto[]>("/api/v1/roots");
        list.Should().Contain(r => r.Id == root!.Id && r.Exists);

        var put = await client.PutAsJsonAsync($"/api/v1/roots/{root!.Id}", new { path = _dir, enabled = false });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var del = await client.DeleteAsync($"/api/v1/roots/{root.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traversalPath = Path.Combine(_dir, "sub", "..");
        var traversal = await client.PostAsJsonAsync("/api/v1/roots", new { path = traversalPath });
        traversal.StatusCode.Should().Be(HttpStatusCode.Created);
        var resolved = await traversal.Content.ReadFromJsonAsync<RootDto>();
        resolved!.Path.Should().Be(PathNormalizer.Normalize(_dir));
    }

    private sealed record ValidateDto(bool Exists, string[] SampleFiles, int MediaFileCountSample);
    private sealed record RootDto(int Id, string Path, bool Enabled, DateTime? LastScanAt, bool Exists);
}
