using System.Collections.Generic;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    // Radarr v3 API response models
    public class RadarrMovie
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public bool Monitored { get; set; }
        public RadarrMovieFile MovieFile { get; set; }
        public RadarrQualityProfileRef QualityProfile { get; set; }
        public List<int> Tags { get; set; }
    }

    public class RadarrMovieFile
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
    }

    public class RadarrQualityProfileRef
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class RadarrQualityProfile
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // Sonarr v3 API response models
    public class SonarrSeries
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public bool Monitored { get; set; }
        public int QualityProfileId { get; set; }
        public List<int> Tags { get; set; }
    }

    public class SonarrEpisodeFile
    {
        public int Id { get; set; }
        public int SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
    }

    public class SonarrQualityProfile
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // Shared models
    public class ArrSystemStatus
    {
        public string Version { get; set; }
        public string AppName { get; set; }
    }

    public class ArrCommand
    {
        public string Name { get; set; }
        public List<int> MovieIds { get; set; }
        public List<int> EpisodeIds { get; set; }
    }

    public class ArrConnectionTestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Version { get; set; }
    }

    public class FileMatchResult
    {
        public MediaFile MediaFile { get; set; }
        public MediaSource Source { get; set; }
        public RadarrMovie RadarrMovie { get; set; }
        public SonarrSeries SonarrSeries { get; set; }
        public SonarrEpisodeFile SonarrEpisodeFile { get; set; }
    }
}
