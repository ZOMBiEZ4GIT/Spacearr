namespace Spacearr.Scanning;

public sealed record ProbeResult(
    double? DurationSeconds,
    int? Width,
    int? Height,
    double? FrameRate,
    string? VideoCodec,
    string? VideoProfile,
    int? BitDepth,
    long? OverallBitrateBps,
    long? VideoBitrateBps,
    string? Container,
    string? AudioSummary,
    long? AudioBitrateBps,
    string? HdrFormat);

public sealed class ProbeException : Exception
{
    public ProbeException(string message, Exception? inner = null) : base(message, inner) { }
}
