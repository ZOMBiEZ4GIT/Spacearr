using Spacearr.Arr;
using Spacearr.Scanning;

namespace Spacearr.Jobs;

public static class JobServiceExtensions
{
    /// <summary>
    /// Registers the job queue, the progress hub, the pluggable job factories
    /// and the two hosted services that drain the queue and schedule scans.
    /// </summary>
    public static IServiceCollection AddSpacearrJobs(this IServiceCollection services)
    {
        services.AddSingleton<IProgressHub, ProgressHub>();
        services.AddSingleton<IJobQueue, JobQueue>();
        services.AddSingleton<IJobFactories>(new JobFactories
        {
            Scan = sp => sp.GetRequiredService<ScanJob>(),
            Enrich = sp => sp.GetRequiredService<EnrichJob>(),
        });
        services.AddHostedService<JobRunner>();
        services.AddHostedService<ScanScheduler>();
        return services;
    }
}
