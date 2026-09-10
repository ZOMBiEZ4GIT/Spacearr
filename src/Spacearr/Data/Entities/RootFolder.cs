namespace Spacearr.Data.Entities;

public sealed class RootFolder
{
    public int Id { get; set; }
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime? LastScanAt { get; set; }
}
