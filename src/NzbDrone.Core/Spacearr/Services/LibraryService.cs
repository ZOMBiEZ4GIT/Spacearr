using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Spacearr.Services
{
    public class LibraryItem
    {
        public MediaItem MediaItem { get; set; }
        public MediaFile MediaFile { get; set; }
    }

    public class LibraryStats
    {
        public long TotalSizeBytes { get; set; }
        public int FileCount { get; set; }
        public int MovieCount { get; set; }
        public int SeriesCount { get; set; }
        public Dictionary<string, long> SizeByQuality { get; set; }
    }

    public interface ILibraryService
    {
        List<LibraryItem> GetAll();
        List<LibraryItem> GetFiltered(MediaSource? source, long? minSize, string sortBy, bool ascending);
        LibraryItem Get(int mediaItemId);
        LibraryStats GetStats();
    }

    public class LibraryService : ILibraryService
    {
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IMediaFileRepository _mediaFileRepository;

        public LibraryService(IMediaItemRepository mediaItemRepository, IMediaFileRepository mediaFileRepository)
        {
            _mediaItemRepository = mediaItemRepository;
            _mediaFileRepository = mediaFileRepository;
        }

        public List<LibraryItem> GetAll()
        {
            var items = _mediaItemRepository.All().ToList();
            var files = _mediaFileRepository.All().ToDictionary(f => f.Id);

            return items.Select(i => new LibraryItem
            {
                MediaItem = i,
                MediaFile = files.ContainsKey(i.MediaFileId) ? files[i.MediaFileId] : null
            }).ToList();
        }

        public List<LibraryItem> GetFiltered(MediaSource? source, long? minSize, string sortBy, bool ascending)
        {
            var all = GetAll();

            if (source.HasValue && source.Value != MediaSource.Unknown)
            {
                all = all.Where(x => x.MediaItem.Source == source.Value).ToList();
            }

            if (minSize.HasValue)
            {
                all = all.Where(x => x.MediaFile != null && x.MediaFile.SizeBytes >= minSize.Value).ToList();
            }

            all = sortBy?.ToLowerInvariant() switch
            {
                "size" => ascending ? all.OrderBy(x => x.MediaFile?.SizeBytes ?? 0).ToList()
                                    : all.OrderByDescending(x => x.MediaFile?.SizeBytes ?? 0).ToList(),
                "title" => ascending ? all.OrderBy(x => x.MediaItem.Title).ToList()
                                     : all.OrderByDescending(x => x.MediaItem.Title).ToList(),
                "bitrate" => ascending ? all.OrderBy(x => x.MediaFile?.BitrateBps ?? 0).ToList()
                                       : all.OrderByDescending(x => x.MediaFile?.BitrateBps ?? 0).ToList(),
                _ => all
            };

            return all;
        }

        public LibraryItem Get(int mediaItemId)
        {
            var item = _mediaItemRepository.Get(mediaItemId);

            if (item == null)
            {
                return null;
            }

            var file = _mediaFileRepository.Find(item.MediaFileId);

            return new LibraryItem
            {
                MediaItem = item,
                MediaFile = file
            };
        }

        public LibraryStats GetStats()
        {
            var all = GetAll();

            return new LibraryStats
            {
                TotalSizeBytes = all.Sum(x => x.MediaFile?.SizeBytes ?? 0),
                FileCount = all.Count(x => x.MediaFile != null),
                MovieCount = all.Count(x => x.MediaItem.Source == MediaSource.Radarr),
                SeriesCount = all.Where(x => x.MediaItem.Source == MediaSource.Sonarr)
                                 .Select(x => x.MediaItem.SeriesTitle)
                                 .Distinct()
                                 .Count(),
                SizeByQuality = all.GroupBy(x => x.MediaItem.QualityProfile ?? "Unknown")
                                   .ToDictionary(g => g.Key, g => g.Sum(x => x.MediaFile?.SizeBytes ?? 0))
            };
        }
    }
}
