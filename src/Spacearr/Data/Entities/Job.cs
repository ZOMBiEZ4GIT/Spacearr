namespace Spacearr.Data.Entities;

public sealed class Job
{
    public int Id { get; set; }
    public JobType Type { get; set; }
    public JobStatus Status { get; set; }
    public JobTrigger Trigger { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Summary { get; set; }
    public string? Error { get; set; }
}
