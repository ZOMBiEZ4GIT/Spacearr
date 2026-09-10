using System.Security.Cryptography;
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

    public IArrClient Create(ArrInstance instance) => Create(instance.Type, instance.BaseUrl, Unprotect(instance.ApiKeyEncrypted));

    public IArrClient Create(ArrType type, string baseUrl, string apiKey)
    {
        var http = _httpFactory.CreateClient("arr");
        // Both failures below are stored-configuration problems, not bugs: they must
        // reach the user as an ArrException ("test this connection failed, here is why")
        // rather than escaping as a 500 from whichever endpoint happened to call us.
        try { http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"); }
        catch (UriFormatException ex) { throw new ArrException(null, "The stored URL for this connection is invalid. Edit the connection and enter a full URL.", ex); }
        return type == ArrType.Radarr ? new RadarrClient(http, apiKey) : new SonarrClient(http, apiKey);
    }

    private string Unprotect(string encrypted)
    {
        try { return _secrets.Unprotect(encrypted); }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new ArrException(null, "Could not decrypt the stored API key (secret.key changed?). Edit the connection and re-enter the key.", ex);
        }
    }
}
