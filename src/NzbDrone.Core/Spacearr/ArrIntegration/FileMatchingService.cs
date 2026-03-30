using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NLog;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    public interface IFileMatchingService
    {
        List<FileMatchResult> MatchFiles(
            List<MediaFile> mediaFiles,
            List<RadarrMovie> radarrMovies,
            List<SonarrSeries> sonarrSeries,
            Dictionary<int, List<SonarrEpisodeFile>> sonarrEpisodeFiles);
    }

    public class FileMatchingService : IFileMatchingService
    {
        private readonly Logger _logger;

        public FileMatchingService(Logger logger)
        {
            _logger = logger;
        }

        public List<FileMatchResult> MatchFiles(
            List<MediaFile> mediaFiles,
            List<RadarrMovie> radarrMovies,
            List<SonarrSeries> sonarrSeries,
            Dictionary<int, List<SonarrEpisodeFile>> sonarrEpisodeFiles)
        {
            var results = new List<FileMatchResult>();

            // Build lookup dictionaries with normalized paths for fast matching
            var radarrByPath = new Dictionary<string, RadarrMovie>();
            foreach (var movie in radarrMovies)
            {
                if (movie.MovieFile?.Path != null)
                {
                    var normalizedPath = NormalizePath(movie.MovieFile.Path);
                    radarrByPath[normalizedPath] = movie;
                }
            }

            var sonarrSeriesById = sonarrSeries.ToDictionary(s => s.Id);
            var sonarrEpisodeByPath = new Dictionary<string, (SonarrSeries Series, SonarrEpisodeFile EpisodeFile)>();
            foreach (var kvp in sonarrEpisodeFiles)
            {
                sonarrSeriesById.TryGetValue(kvp.Key, out var series);

                foreach (var episodeFile in kvp.Value)
                {
                    if (episodeFile.Path != null && series != null)
                    {
                        var normalizedPath = NormalizePath(episodeFile.Path);
                        sonarrEpisodeByPath[normalizedPath] = (series, episodeFile);
                    }
                }
            }

            foreach (var mediaFile in mediaFiles)
            {
                var normalizedMediaPath = NormalizePath(mediaFile.Path);

                if (radarrByPath.TryGetValue(normalizedMediaPath, out var radarrMovie))
                {
                    results.Add(new FileMatchResult
                    {
                        MediaFile = mediaFile,
                        Source = MediaSource.Radarr,
                        RadarrMovie = radarrMovie
                    });

                    _logger.Trace("Matched file '{0}' to Radarr movie '{1}'", mediaFile.Path, radarrMovie.Title);
                }
                else if (sonarrEpisodeByPath.TryGetValue(normalizedMediaPath, out var sonarrMatch))
                {
                    results.Add(new FileMatchResult
                    {
                        MediaFile = mediaFile,
                        Source = MediaSource.Sonarr,
                        SonarrSeries = sonarrMatch.Series,
                        SonarrEpisodeFile = sonarrMatch.EpisodeFile
                    });

                    _logger.Trace("Matched file '{0}' to Sonarr series '{1}' S{2:D2}", mediaFile.Path, sonarrMatch.Series.Title, sonarrMatch.EpisodeFile.SeasonNumber);
                }
                else
                {
                    results.Add(new FileMatchResult
                    {
                        MediaFile = mediaFile,
                        Source = MediaSource.Unknown
                    });

                    _logger.Trace("No match found for file '{0}'", mediaFile.Path);
                }
            }

            _logger.Info("File matching complete: {0} total, {1} Radarr, {2} Sonarr, {3} unmatched",
                results.Count,
                results.Count(r => r.Source == MediaSource.Radarr),
                results.Count(r => r.Source == MediaSource.Sonarr),
                results.Count(r => r.Source == MediaSource.Unknown));

            return results;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            // Replace backslashes with forward slashes
            var normalized = path.Replace('\\', '/');

            // Remove trailing slashes
            normalized = normalized.TrimEnd('/');

            // Case-insensitive on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                normalized = normalized.ToLowerInvariant();
            }

            return normalized;
        }
    }
}
