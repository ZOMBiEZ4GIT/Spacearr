using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Spacearr
{
    public class ActionHistory : ModelBase
    {
        public ActionType ActionType { get; set; }
        public int? MediaItemId { get; set; }
        public string Title { get; set; }
        public string Details { get; set; }
        public string OldQuality { get; set; }
        public string NewQuality { get; set; }
        public long SpaceFreedBytes { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
