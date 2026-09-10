using Spacearr.Data.Entities;
using Spacearr.Infrastructure;

namespace Spacearr.Arr;

public interface IArrClientFactory
{
    IArrClient Create(ArrInstance instance);
    IArrClient Create(ArrType type, string baseUrl, string apiKey);
}

public sealed class ArrClientFactory : IArrClientFactory
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ISecretProtector _secrets;

    public ArrClientFactory(IHttpClientFactory httpFactory, ISecretProtector secrets) { _httpFactory = httpFactory; _secrets = secrets; }

    public IArrClient Create(ArrInstance instance) => Create(instance.Type, instance.BaseUrl, _secrets.Unprotect(instance.ApiKeyEncrypted));

    public IArrClient Create(ArrType type, string baseUrl, string apiKey)
    {
        var http = _httpFactory.CreateClient("arr");
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        return type == ArrType.Radarr ? new RadarrClient(http, apiKey) : new SonarrClient(http, apiKey);
    }
}
