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
        services.AddSingleton<IJobFactories, JobFactories>();
        services.AddHostedService<JobRunner>();
        services.AddHostedService<ScanScheduler>();
        return services;
    }
}
