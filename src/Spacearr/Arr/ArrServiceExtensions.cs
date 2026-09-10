namespace Spacearr.Arr;

public static class ArrServiceExtensions
{
    public static IServiceCollection AddSpacearrArr(this IServiceCollection services)
    {
        services.AddHttpClient("arr", c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IArrClientFactory, ArrClientFactory>();
        services.AddMemoryCache();
        return services;
    }
}
