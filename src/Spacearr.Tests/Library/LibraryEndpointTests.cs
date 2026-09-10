using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Spacearr.Tests.Arr;

namespace Spacearr.Tests.Library;

public class LibraryEndpointTests : IClassFixture<ArrTestApp>
{
    private readonly ArrTestApp _app;
    public LibraryEndpointTests(ArrTestApp app) => _app = app;

    [Fact]
    public async Task List_sorts_by_size_and_filters_and_pages()
    {
        var (instanceId, _) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&sort=size&order=desc&pageSize=2");
        page.GetProperty("total").GetInt32().Should().Be(6);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items[0].GetProperty("title").GetString().Should().Be("M6");
        items[0].GetProperty("heat").GetDouble().Should().BeGreaterThan(items[1].GetProperty("heat").GetDouble());
        items[0].GetProperty("color").GetString().Should().StartWith("#");

        var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library?instanceId={instanceId}&search=M3");
        filtered.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Tree_and_stats_and_detail()
    {
        var (instanceId, itemIds) = await Seed.LibraryAsync(_app);
        var client = await AuthedClient.CreateAsync(_app);

        var tree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/tree?instanceId={instanceId}&colorBy=heat");
        tree.GetProperty("name").GetString().Should().Be("Library");
        tree.GetProperty("children").GetArrayLength().Should().Be(6);
        tree.GetProperty("children")[0].GetProperty("leaf").GetProperty("color").GetString().Should().Be("#D14D4D");

        var stats = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/stats?instanceId={instanceId}");
        stats.GetProperty("fileCount").GetInt32().Should().Be(6);
        stats.GetProperty("byQuality").EnumerateArray().Should().HaveCount(2);
        stats.GetProperty("largest").EnumerateArray().First().GetProperty("title").GetString().Should().Be("M6");
        stats.GetProperty("heatHistogram").GetArrayLength().Should().Be(10);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/library/{itemIds[5]}");
        detail.GetProperty("item").GetProperty("title").GetString().Should().Be("M6");
        var profiles = detail.GetProperty("profiles").EnumerateArray().ToList();
        profiles.Should().NotContain(p => p.GetProperty("id").GetInt32() == 5, "the current profile is excluded");
        var hd = profiles.Single(p => p.GetProperty("name").GetString() == "HD-1080p");
        hd.GetProperty("estimate").GetProperty("basis").GetString().Should().BeOneOf("library", "table", "unknown");
    }
}
