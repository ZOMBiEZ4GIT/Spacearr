using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Spacearr
{
    public class ScanJob : ModelBase
    {
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ScanStatus Status { get; set; }
        public int FilesScanned { get; set; }
        public int FilesAdded { get; set; }
        public int FilesRemoved { get; set; }
        public string ErrorMessage { get; set; }
    }
}
