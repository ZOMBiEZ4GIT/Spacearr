namespace Spacearr.Library;

public static class Heat
{
    public const string UnknownColor = "#6B7280";

    public static double CodecFactor(string? codec) => (codec ?? "").ToLowerInvariant() switch
    {
        "h264" or "avc" or "avc1" => 1.00,
        "hevc" or "h265" or "hvc1" => 0.60,
        "av1" => 0.50,
        "vp9" => 0.65,
        "mpeg2video" or "mpeg2" => 1.50,
        "vc1" or "vc-1" => 1.20,
        _ => 1.00,
    };

    public static double? Bpp(long? videoBps, int? width, int? height, double? fps)
    {
        if (videoBps is null or <= 0 || width is null or <= 0 || height is null or <= 0 || fps is null or <= 0) return null;
        return videoBps.Value / ((double)width.Value * height.Value * fps.Value);
    }

    public static double? NormalisedBpp(long? videoBps, int? width, int? height, double? fps, string? codec)
    {
        var bpp = Bpp(videoBps, width, height, fps);
        return bpp is null ? null : bpp / CodecFactor(codec);
    }

    private static readonly (double X, double Y)[] AbsoluteStops = { (0.04, 0.0), (0.10, 0.33), (0.20, 0.66), (0.30, 1.0) };

    public static double AbsoluteHeat(double nbpp)
    {
        if (nbpp <= AbsoluteStops[0].X) return 0;
        if (nbpp >= AbsoluteStops[^1].X) return 1;
        for (var i = 1; i < AbsoluteStops.Length; i++)
        {
            var (x0, y0) = AbsoluteStops[i - 1];
            var (x1, y1) = AbsoluteStops[i];
            if (nbpp <= x1) return y0 + (y1 - y0) * (nbpp - x0) / (x1 - x0);
        }
        return 1;
    }

    public static double[] RelativeHeat(double?[] nbpp)
    {
        var result = new double[nbpp.Length];
        var known = nbpp.Select((v, i) => (v, i)).Where(t => t.v is not null).OrderBy(t => t.v).ToList();
        if (known.Count == 0) return Enumerable.Repeat(double.NaN, nbpp.Length).ToArray();
        for (var i = 0; i < nbpp.Length; i++) result[i] = double.NaN;
        if (known.Count == 1) { result[known[0].i] = 0.5; return result; }
        // average rank for ties
        var pos = 0;
        while (pos < known.Count)
        {
            var end = pos;
            while (end + 1 < known.Count && known[end + 1].v == known[pos].v) end++;
            var rank = (pos + end) / 2.0 / (known.Count - 1);
            for (var k = pos; k <= end; k++) result[known[k].i] = rank;
            pos = end + 1;
        }
        return result;
    }

    private static readonly (int R, int G, int B)[] Ramp = { (0x2E, 0x8B, 0x57), (0xC9, 0xA2, 0x27), (0xE0, 0x7B, 0x1F), (0xD1, 0x4D, 0x4D) };

    public static string Color(double heat)
    {
        if (double.IsNaN(heat)) return UnknownColor;
        var t = Math.Clamp(heat, 0, 1) * (Ramp.Length - 1);
        var i = Math.Min((int)Math.Floor(t), Ramp.Length - 2);
        var f = t - i;
        var (r0, g0, b0) = Ramp[i]; var (r1, g1, b1) = Ramp[i + 1];
        int L(int a, int b) => (int)Math.Round(a + (b - a) * f);
        return $"#{L(r0, r1):X2}{L(g0, g1):X2}{L(b0, b1):X2}";
    }

    private static readonly string[] Palette = { "#4C78A8", "#F58518", "#54A24B", "#B279A2", "#FF9DA6", "#9D755D", "#BAB0AC", "#E45756", "#72B7B2", "#EECA3B", "#3B6D9E", "#A0522D" };

    public static string CategoryColor(string? value)
    {
        if (string.IsNullOrEmpty(value)) return UnknownColor;
        var hash = 0;
        foreach (var c in value) hash = unchecked(hash * 31 + c);
        return Palette[Math.Abs(hash) % Palette.Length];
    }
}
