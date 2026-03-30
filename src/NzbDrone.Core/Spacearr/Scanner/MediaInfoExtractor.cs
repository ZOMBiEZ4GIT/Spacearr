using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Processes;

namespace NzbDrone.Core.Spacearr.Scanner
{
    public class ExtractedMediaInfo
    {
        public long BitrateBps { get; set; }
        public string Codec { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int DurationSeconds { get; set; }
        public string ContainerFormat { get; set; }
    }

    public interface IMediaInfoExtractor
    {
        ExtractedMediaInfo Extract(string filePath);
    }

    public class MediaInfoExtractor : IMediaInfoExtractor
    {
        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        private bool? _mediaInfoAvailable;
        private bool? _ffprobeAvailable;

        public MediaInfoExtractor(IProcessProvider processProvider, Logger logger)
        {
            _processProvider = processProvider;
            _logger = logger;
        }

        public ExtractedMediaInfo Extract(string filePath)
        {
            try
            {
                var result = TryMediaInfo(filePath);

                if (result != null)
                {
                    return result;
                }

                result = TryFfprobe(filePath);

                if (result != null)
                {
                    return result;
                }

                _logger.Warn("Neither mediainfo nor ffprobe could extract metadata from: {0}", filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error extracting media info from: {0}", filePath);
            }

            return null;
        }

        private ExtractedMediaInfo TryMediaInfo(string filePath)
        {
            if (_mediaInfoAvailable.HasValue && !_mediaInfoAvailable.Value)
            {
                return null;
            }

            try
            {
                var output = _processProvider.StartAndCapture("mediainfo", $"--Output=JSON \"{filePath}\"");

                if (output.ExitCode != 0)
                {
                    _logger.Debug("mediainfo returned exit code {0} for {1}", output.ExitCode, filePath);
                    return null;
                }

                var json = string.Join("", output.Standard.Select(l => l.Content));

                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                _mediaInfoAvailable = true;

                return ParseMediaInfoJson(json, filePath);
            }
            catch (Win32Exception)
            {
                _logger.Debug("mediainfo is not available on this system");
                _mediaInfoAvailable = false;
                return null;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to run mediainfo for {0}", filePath);
                return null;
            }
        }

        private ExtractedMediaInfo TryFfprobe(string filePath)
        {
            if (_ffprobeAvailable.HasValue && !_ffprobeAvailable.Value)
            {
                return null;
            }

            try
            {
                var output = _processProvider.StartAndCapture("ffprobe", $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"");

                if (output.ExitCode != 0)
                {
                    _logger.Debug("ffprobe returned exit code {0} for {1}", output.ExitCode, filePath);
                    return null;
                }

                var json = string.Join("", output.Standard.Select(l => l.Content));

                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                _ffprobeAvailable = true;

                return ParseFfprobeJson(json, filePath);
            }
            catch (Win32Exception)
            {
                _logger.Debug("ffprobe is not available on this system");
                _ffprobeAvailable = false;
                return null;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to run ffprobe for {0}", filePath);
                return null;
            }
        }

        private ExtractedMediaInfo ParseMediaInfoJson(string json, string filePath)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var result = new ExtractedMediaInfo();

                if (!root.TryGetProperty("media", out var media) ||
                    !media.TryGetProperty("track", out var tracks))
                {
                    return null;
                }

                foreach (var track in tracks.EnumerateArray())
                {
                    var trackType = track.GetPropertyStringOrDefault("@type");

                    if (string.Equals(trackType, "General", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ContainerFormat = track.GetPropertyStringOrDefault("Format");
                        result.BitrateBps = ParseLong(track.GetPropertyStringOrDefault("OverallBitRate"));
                        result.DurationSeconds = (int)ParseDouble(track.GetPropertyStringOrDefault("Duration"));
                    }
                    else if (string.Equals(trackType, "Video", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Codec = track.GetPropertyStringOrDefault("Format");
                        result.Width = (int)ParseLong(track.GetPropertyStringOrDefault("Width"));
                        result.Height = (int)ParseLong(track.GetPropertyStringOrDefault("Height"));

                        if (result.BitrateBps == 0)
                        {
                            result.BitrateBps = ParseLong(track.GetPropertyStringOrDefault("BitRate"));
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to parse mediainfo JSON for {0}", filePath);
                return null;
            }
        }

        private ExtractedMediaInfo ParseFfprobeJson(string json, string filePath)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var result = new ExtractedMediaInfo();

                if (root.TryGetProperty("format", out var format))
                {
                    result.ContainerFormat = format.GetPropertyStringOrDefault("format_name");
                    result.BitrateBps = ParseLong(format.GetPropertyStringOrDefault("bit_rate"));
                    result.DurationSeconds = (int)ParseDouble(format.GetPropertyStringOrDefault("duration"));
                }

                if (root.TryGetProperty("streams", out var streams))
                {
                    foreach (var stream in streams.EnumerateArray())
                    {
                        var codecType = stream.GetPropertyStringOrDefault("codec_type");

                        if (string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Codec = stream.GetPropertyStringOrDefault("codec_name");

                            if (stream.TryGetProperty("width", out var widthEl))
                            {
                                result.Width = widthEl.GetInt32();
                            }

                            if (stream.TryGetProperty("height", out var heightEl))
                            {
                                result.Height = heightEl.GetInt32();
                            }

                            break;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to parse ffprobe JSON for {0}", filePath);
                return null;
            }
        }

        private static long ParseLong(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            return long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        private static double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
    }

    internal static class JsonElementExtensions
    {
        public static string GetPropertyStringOrDefault(this JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value.ValueKind == JsonValueKind.Number
                    ? value.GetRawText()
                    : value.GetString();
            }

            return null;
        }
    }
}
