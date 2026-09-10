using Spacearr.Data.Entities;

namespace Spacearr.Jobs;

/// <summary>
/// Indirection so the plumbing (queue, runner, endpoints) can be wired and
/// tested before the real Scan/Enrich jobs exist, and so tests can substitute
/// their own job implementations.
/// </summary>
public interface IJobFactories
{
    Func<IServiceProvider, IJob> Scan { get; }
    Func<IServiceProvider, IJob> Enrich { get; }
}

public sealed class JobFactories : IJobFactories
{
    public Func<IServiceProvider, IJob> Scan { get; init; } = _ => new NoOpJob(JobType.Scan);
    public Func<IServiceProvider, IJob> Enrich { get; init; } = _ => new NoOpJob(JobType.Enrich);
}
