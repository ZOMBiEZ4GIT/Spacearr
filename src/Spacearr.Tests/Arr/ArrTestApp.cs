using Microsoft.Extensions.DependencyInjection;

namespace Spacearr.Tests.Arr;

public class ArrTestApp : TestApp
{
    public FakeArrHandler Arr { get; } = new FakeArrHandler()
        .MapFixture("GET", "/api/v3/system/status", "arr-status.json")
        .MapFixture("GET", "/api/v3/qualityprofile", "radarr-qualityprofile.json")
        .MapFixture("GET", "/api/v3/tag", "radarr-tag.json")
        .Map("GET", "/api/v3/rootfolder", """[{"path":"/data/movies"}]""")
        .MapFixture("GET", "/api/v3/movie", "radarr-movie.json")
        .Map("PUT", "/api/v3/movie/10", "{}")
        .Map("GET", "/api/v3/movie/10", FakeArrHandler.First("radarr-movie.json"))
        .Map("DELETE", "/api/v3/moviefile/77", "{}")
        .Map("POST", "/api/v3/command", """{"id":1}""");

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddHttpClient("arr").ConfigurePrimaryHttpMessageHandler(() => Arr);
    }
}
