namespace Spacearr.Data.Entities;

public sealed class ActionLog
{
    public int Id { get; set; }
    public DateTime At { get; set; }
    public ActionType Type { get; set; }
    public int? MediaItemId { get; set; }
    public int ArrInstanceId { get; set; }
    public string Title { get; set; } = "";
    public string? Path { get; set; }
    public long SizeBytesBefore { get; set; }
    public string? QualityBefore { get; set; }
    public string? QualityAfter { get; set; }
    public ActionOutcome Outcome { get; set; }
    public string? Detail { get; set; }
}
