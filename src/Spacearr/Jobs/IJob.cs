using Spacearr.Data.Entities;

namespace Spacearr.Jobs;

public interface IJob
{
    JobType Type { get; }
    Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct);
}

public sealed record JobSummary(
    int FilesSeen = 0,
    int FilesProbed = 0,
    int FilesAdded = 0,
    int FilesRemoved = 0,
    int ItemsMatched = 0,
    int ItemsUnmatched = 0,
    string[]? Errors = null)
{
    public string[] Errors { get; init; } = Errors ?? Array.Empty<string>();
}

public sealed record JobContext(int JobId, JobType Type, IServiceProvider Services, IProgressHub Progress)
{
    public void Report(string phase, int done, int total, string? detail = null) =>
        Progress.Publish(new ProgressEvent(JobId, Type, "progress", phase, done, total, detail, null));
}

public sealed record ProgressEvent(int JobId, JobType Type, string Kind, string? Phase, int Done, int Total, string? Detail, JobStatus? Status);

public sealed class NoOpJob : IJob
{
    public NoOpJob(JobType type) => Type = type;
    public JobType Type { get; }
    public Task<JobSummary> RunAsync(JobContext ctx, CancellationToken ct)
    {
        ctx.Report("noop", 1, 1);
        return Task.FromResult(new JobSummary());
    }
}
