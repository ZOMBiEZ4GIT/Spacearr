namespace Spacearr.Scanning;

public static class ScanningServiceExtensions
{
    /// <summary>Registers the tool locator and the ffprobe-backed media prober.</summary>
    public static IServiceCollection AddSpacearrScanning(this IServiceCollection services)
    {
        services.AddSingleton<IToolLocator, ToolLocator>();
        services.AddSingleton<IMediaProber, FfprobeProber>();
        services.AddSingleton<IFileDiscovery, FileDiscovery>();
        services.AddScoped<ScanJob>();
        services.AddScoped<IEnrichRunner, NoEnrichRunner>();
        return services;
    }
}
