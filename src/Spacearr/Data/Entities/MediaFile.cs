namespace Spacearr.Data.Entities;

public sealed class MediaFile
{
    public int Id { get; set; }
    public string Path { get; set; } = "";
    public int RootFolderId { get; set; }
    public long SizeBytes { get; set; }
    public DateTime ModifiedAt { get; set; }
    public DateTime ScannedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? FrameRate { get; set; }
    public string? VideoCodec { get; set; }
    public string? VideoProfile { get; set; }
    public int? BitDepth { get; set; }
    public long? OverallBitrateBps { get; set; }
    public long? VideoBitrateBps { get; set; }
    public string? Container { get; set; }
    public string? AudioSummary { get; set; }
    public long? AudioBitrateBps { get; set; }
    public string? HdrFormat { get; set; }
    public string? ProbeError { get; set; }
}
