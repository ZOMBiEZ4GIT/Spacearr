namespace Spacearr.Data.Entities;

public sealed class ArrInstance
{
    public int Id { get; set; }
    public ArrType Type { get; set; }
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKeyEncrypted { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PathMapping> PathMappings { get; set; } = new();
}
