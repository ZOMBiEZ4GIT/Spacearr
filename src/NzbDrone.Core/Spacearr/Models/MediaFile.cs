using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Spacearr
{
    public class MediaFile : ModelBase
    {
        public string Path { get; set; }
        public long SizeBytes { get; set; }
        public long BitrateBps { get; set; }
        public string Codec { get; set; }
        public string Resolution { get; set; }
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        public int DurationSeconds { get; set; }
        public string ContainerFormat { get; set; }
        public string LibraryPath { get; set; }
        public DateTime LastScanned { get; set; }
    }
}
