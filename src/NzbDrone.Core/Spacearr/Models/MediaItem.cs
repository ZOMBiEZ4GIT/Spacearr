using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Spacearr
{
    public class MediaItem : ModelBase
    {
        public int MediaFileId { get; set; }
        public MediaSource Source { get; set; }
        public int ExternalId { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string SeriesTitle { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string QualityProfile { get; set; }
        public bool Monitored { get; set; }
        public string Tags { get; set; }
        public DateTime LastEnriched { get; set; }
    }
}
