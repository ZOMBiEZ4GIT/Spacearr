namespace Spacearr.Library;

public sealed record SavingsEstimate(long EstimatedBytes, long SavingsBytes, int Samples, string Basis);

public static class SavingsEstimator
{
    private static readonly Dictionary<string, long> TableBytesPerSecond = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Remux-2160p"] = 10_000_000, ["Bluray-2160p"] = 8_000_000, ["WEBDL-2160p"] = 4_000_000, ["WEBRip-2160p"] = 4_000_000, ["HDTV-2160p"] = 3_000_000,
        ["Remux-1080p"] = 4_500_000, ["Bluray-1080p"] = 3_000_000, ["WEBDL-1080p"] = 1_200_000, ["WEBRip-1080p"] = 1_200_000, ["HDTV-1080p"] = 1_000_000,
        ["Bluray-720p"] = 1_200_000, ["WEBDL-720p"] = 600_000, ["WEBRip-720p"] = 600_000, ["HDTV-720p"] = 500_000,
        ["SDTV"] = 250_000, ["DVD"] = 350_000, ["WEBDL-480p"] = 250_000,
    };

    public static SavingsEstimate Estimate(LibraryRow target, string targetQualityName, IReadOnlyList<LibraryRow> library)
    {
        var duration = target.DurationSeconds ?? 0;
        if (duration <= 0) return new SavingsEstimate(target.SizeBytes, 0, 0, "unknown");

        var samples = library
            .Where(r => r.FileId != target.FileId && r.InstanceType == target.InstanceType && r.DurationSeconds is > 0 && r.ProbeError is null
                        && string.Equals(r.QualityName, targetQualityName, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.SizeBytes / r.DurationSeconds!.Value).OrderBy(x => x).ToList();

        long estimated; string basis;
        if (samples.Count >= 5)
        {
            var median = samples.Count % 2 == 1 ? samples[samples.Count / 2] : (samples[samples.Count / 2 - 1] + samples[samples.Count / 2]) / 2;
            estimated = (long)(median * duration); basis = "library";
        }
        else if (TableBytesPerSecond.TryGetValue(targetQualityName, out var bps)) { estimated = (long)(bps * duration); basis = "table"; }
        else return new SavingsEstimate(target.SizeBytes, 0, 0, "unknown");

        return new SavingsEstimate(estimated, Math.Max(0, target.SizeBytes - estimated), samples.Count >= 5 ? samples.Count : 0, basis);
    }
}
