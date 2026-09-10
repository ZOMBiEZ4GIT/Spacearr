using FluentAssertions;
using Spacearr.Scanning;

namespace Spacearr.Tests.Scanning;

public class FfprobeParserTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parses_hevc_hdr_mkv_with_bps_tags()
    {
        var r = FfprobeParser.Parse(Fixture("ffprobe-hevc-hdr.json"));
        r.VideoCodec.Should().Be("hevc");
        r.VideoProfile.Should().Be("Main 10");
        r.Width.Should().Be(3840);
        r.Height.Should().Be(2160);
        r.FrameRate.Should().BeApproximately(23.976, 0.001);
        r.BitDepth.Should().Be(10);
        r.VideoBitrateBps.Should().Be(52_341_234);
        r.OverallBitrateBps.Should().Be(58_519_000);
        r.DurationSeconds.Should().BeApproximately(7961.184, 0.001);
        r.Container.Should().Be("mkv");
        r.HdrFormat.Should().Be("HDR10");
        r.AudioSummary.Should().Be("TrueHD 7.1, AC3 5.1");
        r.AudioBitrateBps.Should().Be(4_521_000 + 640_000);
    }

    [Fact]
    public void Parses_h264_mp4_with_stream_bit_rate()
    {
        var r = FfprobeParser.Parse(Fixture("ffprobe-h264-sdr.json"));
        r.VideoCodec.Should().Be("h264");
        r.BitDepth.Should().Be(8);
        r.VideoBitrateBps.Should().Be(8_000_000);
        r.Container.Should().Be("mp4");
        r.HdrFormat.Should().BeNull();
        r.AudioSummary.Should().Be("AAC 2.0");
    }

    [Fact]
    public void Video_bitrate_falls_back_to_overall_minus_audio()
    {
        var json = """{"streams":[{"codec_type":"video","codec_name":"h264","width":1280,"height":720,"avg_frame_rate":"25/1"},{"codec_type":"audio","codec_name":"aac","channels":2,"bit_rate":"128000"}],"format":{"format_name":"matroska,webm","duration":"100","bit_rate":"3128000"}}""";
        var r = FfprobeParser.Parse(json);
        r.VideoBitrateBps.Should().Be(3_000_000);
    }

    [Fact]
    public void No_video_stream_yields_nulls_not_exception()
    {
        var r = FfprobeParser.Parse("""{"streams":[{"codec_type":"audio","codec_name":"flac"}],"format":{"format_name":"flac","duration":"10"}}""");
        r.VideoCodec.Should().BeNull();
        r.Width.Should().BeNull();
    }

    [Fact]
    public void Garbage_throws_format_exception()
    {
        var act = () => FfprobeParser.Parse("not json");
        act.Should().Throw<FormatException>();
    }
}
