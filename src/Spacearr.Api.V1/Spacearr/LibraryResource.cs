using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class LibraryResource : RestResource
    {
        public string Title { get; set; }
        public int Year { get; set; }
        public string SeriesTitle { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string Source { get; set; }
        public string QualityProfile { get; set; }
        public bool Monitored { get; set; }
        public string FilePath { get; set; }
        public long SizeBytes { get; set; }
        public long BitrateBps { get; set; }
        public string Codec { get; set; }
        public string Resolution { get; set; }
        public int DurationSeconds { get; set; }
        public string ContainerFormat { get; set; }
        public string PosterUrl { get; set; }
    }

    public class LibraryStatsResource
    {
        public long TotalSizeBytes { get; set; }
        public int FileCount { get; set; }
        public int MovieCount { get; set; }
        public int SeriesCount { get; set; }
        public Dictionary<string, long> SizeByQuality { get; set; }
    }

    public static class LibraryResourceMapper
    {
        public static LibraryResource ToResource(this LibraryItem model)
        {
            if (model == null)
            {
                return null;
            }

            return new LibraryResource
            {
                Id = model.MediaItem.Id,
                Title = model.MediaItem.Title,
                Year = model.MediaItem.Year,
                SeriesTitle = model.MediaItem.SeriesTitle,
                SeasonNumber = model.MediaItem.SeasonNumber,
                EpisodeNumber = model.MediaItem.EpisodeNumber,
                Source = model.MediaItem.Source.ToString(),
                QualityProfile = model.MediaItem.QualityProfile,
                Monitored = model.MediaItem.Monitored,
                FilePath = model.MediaFile?.Path,
                SizeBytes = model.MediaFile?.SizeBytes ?? 0,
                BitrateBps = model.MediaFile?.BitrateBps ?? 0,
                Codec = model.MediaFile?.Codec,
                Resolution = model.MediaFile?.Resolution,
                DurationSeconds = model.MediaFile?.DurationSeconds ?? 0,
                ContainerFormat = model.MediaFile?.ContainerFormat,
                PosterUrl = model.MediaItem?.PosterUrl
            };
        }

        public static List<LibraryResource> ToResource(this IEnumerable<LibraryItem> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static LibraryStatsResource ToResource(this LibraryStats stats)
        {
            if (stats == null)
            {
                return null;
            }

            return new LibraryStatsResource
            {
                TotalSizeBytes = stats.TotalSizeBytes,
                FileCount = stats.FileCount,
                MovieCount = stats.MovieCount,
                SeriesCount = stats.SeriesCount,
                SizeByQuality = stats.SizeByQuality
            };
        }
    }
}
