using FluentAssertions;
using Spacearr.Library;

namespace Spacearr.Tests.Library;

public class HeatTests
{
    [Fact]
    public void Bpp_and_codec_normalisation()
    {
        // 8 Mbps 1080p24 h264 -> 8e6 / (1920*1080*24) = 0.1608
        Heat.Bpp(8_000_000, 1920, 1080, 24).Should().BeApproximately(0.1608, 0.001);
        Heat.NormalisedBpp(8_000_000, 1920, 1080, 24, "h264").Should().BeApproximately(0.1608, 0.001);
        Heat.NormalisedBpp(8_000_000, 1920, 1080, 24, "hevc").Should().BeApproximately(0.268, 0.001);
        Heat.Bpp(null, 1920, 1080, 24).Should().BeNull();
        Heat.Bpp(1, 0, 1080, 24).Should().BeNull();
    }

    [Theory]
    [InlineData(0.01, 0.0)]
    [InlineData(0.04, 0.0)]
    [InlineData(0.07, 0.165)]
    [InlineData(0.10, 0.33)]
    [InlineData(0.20, 0.66)]
    [InlineData(0.30, 1.0)]
    [InlineData(0.90, 1.0)]
    public void Absolute_heat_is_piecewise_linear(double nbpp, double expected) =>
        Heat.AbsoluteHeat(nbpp).Should().BeApproximately(expected, 0.01);

    [Fact]
    public void Relative_heat_is_percentile_rank_ignoring_nulls()
    {
        var heat = Heat.RelativeHeat(new double?[] { 0.05, null, 0.20, 0.10, 0.10 });
        heat[0].Should().Be(0.0);
        double.IsNaN(heat[1]).Should().BeTrue();
        heat[2].Should().Be(1.0);
        heat[3].Should().BeApproximately(heat[4], 1e-9);
        heat[3].Should().BeInRange(0.3, 0.7);
    }

    [Fact]
    public void Colour_endpoints_and_midpoint()
    {
        Heat.Color(0).Should().Be("#2E8B57");
        Heat.Color(1).Should().Be("#D14D4D");
        Heat.Color(0.5).Should().NotBe("#2E8B57").And.NotBe("#D14D4D");
        Heat.Color(double.NaN).Should().Be("#6B7280");
    }
}
