using System.Globalization;
using System.Text.Json;

namespace Spacearr.Scanning;

public static class FfprobeParser
{
    private static readonly Dictionary<string, string> AudioNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["truehd"] = "TrueHD", ["ac3"] = "AC3", ["eac3"] = "EAC3", ["dts"] = "DTS", ["aac"] = "AAC",
        ["flac"] = "FLAC", ["opus"] = "Opus", ["mp3"] = "MP3", ["pcm_s16le"] = "PCM", ["pcm_s24le"] = "PCM",
    };

    public static ProbeResult Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new FormatException("ffprobe output is not valid JSON", ex); }

        using (doc)
        {
            var root = doc.RootElement;
            double? duration = null; long? overall = null; string? container = null;
            if (root.TryGetProperty("format", out var format))
            {
                duration = GetDouble(format, "duration");
                overall = GetLong(format, "bit_rate");
                container = ContainerName(GetString(format, "format_name"));
            }

            int? width = null, height = null, bitDepth = null; double? fps = null;
            string? codec = null, profile = null, hdr = null; long? videoBps = null;
            var audioParts = new List<string>(); long audioBps = 0; var audioBpsKnown = false;

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var s in streams.EnumerateArray())
                {
                    var type = GetString(s, "codec_type");
                    if (type == "video" && codec is null)
                    {
                        codec = GetString(s, "codec_name");
                        profile = GetString(s, "profile");
                        width = GetInt(s, "width");
                        height = GetInt(s, "height");
                        fps = ParseRate(GetString(s, "avg_frame_rate")) ?? ParseRate(GetString(s, "r_frame_rate"));
                        bitDepth = GetInt(s, "bits_per_raw_sample") ?? BitDepthFromPixFmt(GetString(s, "pix_fmt"));
                        videoBps = GetLong(s, "bit_rate") ?? GetTagLong(s, "BPS") ?? GetTagLong(s, "BPS-eng");
                        hdr = DetectHdr(s);
                        if (duration is null) duration = ParseDurationTag(GetTagString(s, "DURATION"));
                    }
                    else if (type == "audio")
                    {
                        var name = GetString(s, "codec_name") ?? "audio";
                        var pretty = AudioNames.TryGetValue(name, out var p) ? p : name.ToUpperInvariant();
                        var channels = GetInt(s, "channels");
                        var layout = channels switch { 1 => "1.0", 2 => "2.0", 6 => "5.1", 8 => "7.1", null => "", _ => $"{channels}ch" };
                        audioParts.Add(string.IsNullOrEmpty(layout) ? pretty : $"{pretty} {layout}");
                        var bps = GetLong(s, "bit_rate") ?? GetTagLong(s, "BPS") ?? GetTagLong(s, "BPS-eng");
                        if (bps is not null) { audioBps += bps.Value; audioBpsKnown = true; }
                    }
                }
            }

            if (videoBps is null && overall is not null && codec is not null)
                videoBps = audioBpsKnown ? Math.Max(0, overall.Value - audioBps) : overall;

            return new ProbeResult(duration, width, height, fps, codec, profile, bitDepth, overall, videoBps, container,
                audioParts.Count == 0 ? null : string.Join(", ", audioParts), audioBpsKnown ? audioBps : null, hdr);
        }
    }

    private static string? ContainerName(string? formatName)
    {
        if (formatName is null) return null;
        var first = formatName.Split(',')[0];
        return first switch { "matroska" => "mkv", "mov" => "mp4", "avi" => "avi", "mpegts" => "ts", _ => first };
    }

    private static string? DetectHdr(JsonElement video)
    {
        var transfer = GetString(video, "color_transfer");
        var hasDv = video.TryGetProperty("side_data_list", out var sd) && sd.EnumerateArray().Any(e => (GetString(e, "side_data_type") ?? "").Contains("DOVI", StringComparison.OrdinalIgnoreCase));
        if (hasDv) return "Dolby Vision";
        if (transfer == "smpte2084")
        {
            var hasPlus = video.TryGetProperty("side_data_list", out var sd2) && sd2.EnumerateArray().Any(e => (GetString(e, "side_data_type") ?? "").Contains("HDR Dynamic", StringComparison.OrdinalIgnoreCase));
            return hasPlus ? "HDR10+" : "HDR10";
        }
        if (transfer == "arib-std-b67") return "HLG";
        return null;
    }

    private static int? BitDepthFromPixFmt(string? pixFmt)
    {
        if (pixFmt is null) return null;
        if (pixFmt.Contains("p10")) return 10;
        if (pixFmt.Contains("p12")) return 12;
        if (pixFmt.StartsWith("yuv")) return 8;
        return null;
    }

    private static double? ParseRate(string? rate)
    {
        if (string.IsNullOrEmpty(rate)) return null;
        var parts = rate.Split('/');
        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 && n > 0)
            return n / d;
        return double.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    private static double? ParseDurationTag(string? tag)
    {
        if (tag is null) return null;
        return TimeSpan.TryParse(tag.Length > 16 ? tag[..16] : tag, CultureInfo.InvariantCulture, out var ts) ? ts.TotalSeconds : null;
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null) : null;
    private static int? GetInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : int.TryParse(v.ToString(), out var j) ? j : null) : null;
    private static long? GetLong(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && long.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var l) && l > 0 ? l : null;
    private static double? GetDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : null;
    private static string? GetTagString(JsonElement e, string name) =>
        e.TryGetProperty("tags", out var tags) ? GetString(tags, name) : null;
    private static long? GetTagLong(JsonElement e, string name) =>
        e.TryGetProperty("tags", out var tags) ? GetLong(tags, name) : null;
}
