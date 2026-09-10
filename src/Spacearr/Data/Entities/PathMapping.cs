namespace Spacearr.Data.Entities;

public sealed class PathMapping
{
    public int Id { get; set; }
    public int ArrInstanceId { get; set; }
    public ArrInstance? ArrInstance { get; set; }
    public string RemotePrefix { get; set; } = "";
    public string LocalPrefix { get; set; } = "";
}
