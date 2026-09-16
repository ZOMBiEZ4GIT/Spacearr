using Spacearr.Scanning;

namespace Spacearr.Arr;

public static class ArrServiceExtensions
{
    public static IServiceCollection AddSpacearrArr(this IServiceCollection services)
    {
        // No client-wide timeout: ArrHttp applies a per-request one, so a sync's
        // whole-library listings can outlast the interactive calls (see ArrTimeouts).
        services.AddHttpClient("arr", c => c.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton(ArrTimeouts.Default);
        services.AddSingleton<IArrClientFactory, ArrClientFactory>();
        services.AddMemoryCache();
        services.AddScoped<EnrichJob>();
        services.AddScoped<IEnrichRunner>(sp => sp.GetRequiredService<EnrichJob>());
        return services;
    }
}
