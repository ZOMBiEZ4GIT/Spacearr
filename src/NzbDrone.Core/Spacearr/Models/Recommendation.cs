using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Spacearr
{
    public class Recommendation : ModelBase
    {
        public int MediaItemId { get; set; }
        public RecommendationType RecommendationType { get; set; }
        public long CurrentSizeBytes { get; set; }
        public long EstimatedNewSizeBytes { get; set; }
        public long EstimatedSavingsBytes { get; set; }
        public string SuggestedQuality { get; set; }
        public bool Dismissed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
