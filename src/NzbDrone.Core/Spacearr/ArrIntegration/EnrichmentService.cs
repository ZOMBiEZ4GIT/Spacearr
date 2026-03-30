using System;
using System.Collections.Generic;
using NLog;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    public interface IEnrichmentService
    {
        void EnrichMediaFiles(List<MediaFile> mediaFiles, ArrConnectionSettings radarrSettings, ArrConnectionSettings sonarrSettings);
    }

    public class EnrichmentService : IEnrichmentService
    {
        private readonly IRadarrApiClient _radarrApiClient;
        private readonly ISonarrApiClient _sonarrApiClient;
        private readonly IFileMatchingService _fileMatchingService;
        private readonly IQualityProfileCache _qualityProfileCache;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly Logger _logger;

        public EnrichmentService(
            IRadarrApiClient radarrApiClient,
            ISonarrApiClient sonarrApiClient,
            IFileMatchingService fileMatchingService,
            IQualityProfileCache qualityProfileCache,
            IMediaItemRepository mediaItemRepository,
            Logger logger)
        {
            _radarrApiClient = radarrApiClient;
            _sonarrApiClient = sonarrApiClient;
            _fileMatchingService = fileMatchingService;
            _qualityProfileCache = qualityProfileCache;
            _mediaItemRepository = mediaItemRepository;
            _logger = logger;
        }

        public void EnrichMediaFiles(List<MediaFile> mediaFiles, ArrConnectionSettings radarrSettings, ArrConnectionSettings sonarrSettings)
        {
            _logger.Info("Starting enrichment for {0} media files", mediaFiles.Count);

            var radarrMovies = new List<RadarrMovie>();
            var sonarrSeries = new List<SonarrSeries>();
            var sonarrEpisodeFiles = new Dictionary<int, List<SonarrEpisodeFile>>();

            // Fetch data from Radarr
            if (radarrSettings?.Enabled == true)
            {
                try
                {
                    radarrMovies = _radarrApiClient.GetMovies(radarrSettings);
                    var radarrProfiles = _radarrApiClient.GetQualityProfiles(radarrSettings);
                    _qualityProfileCache.RefreshRadarrProfiles(radarrProfiles);

                    _logger.Info("Fetched {0} movies from Radarr", radarrMovies.Count);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to fetch data from Radarr");
                }
            }

            // Fetch data from Sonarr
            if (sonarrSettings?.Enabled == true)
            {
                try
                {
                    sonarrSeries = _sonarrApiClient.GetSeries(sonarrSettings);
                    var sonarrProfiles = _sonarrApiClient.GetQualityProfiles(sonarrSettings);
                    _qualityProfileCache.RefreshSonarrProfiles(sonarrProfiles);

                    _logger.Info("Fetched {0} series from Sonarr", sonarrSeries.Count);

                    foreach (var series in sonarrSeries)
                    {
                        try
                        {
                            var episodeFiles = _sonarrApiClient.GetEpisodeFiles(sonarrSettings, series.Id);
                            sonarrEpisodeFiles[series.Id] = episodeFiles;
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn(ex, "Failed to fetch episode files for series '{0}' (ID {1})", series.Title, series.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to fetch data from Sonarr");
                }
            }

            // Match files
            var matches = _fileMatchingService.MatchFiles(mediaFiles, radarrMovies, sonarrSeries, sonarrEpisodeFiles);

            // Create/update MediaItem records
            var created = 0;
            var updated = 0;

            foreach (var match in matches)
            {
                try
                {
                    var existingItem = _mediaItemRepository.FindByMediaFileId(match.MediaFile.Id);
                    var mediaItem = existingItem ?? new MediaItem();

                    mediaItem.MediaFileId = match.MediaFile.Id;
                    mediaItem.Source = match.Source;
                    mediaItem.LastEnriched = DateTime.UtcNow;

                    if (match.Source == MediaSource.Radarr && match.RadarrMovie != null)
                    {
                        PopulateFromRadarr(mediaItem, match.RadarrMovie);
                    }
                    else if (match.Source == MediaSource.Sonarr && match.SonarrSeries != null && match.SonarrEpisodeFile != null)
                    {
                        PopulateFromSonarr(mediaItem, match.SonarrSeries, match.SonarrEpisodeFile);
                    }
                    else
                    {
                        mediaItem.Source = MediaSource.Unknown;
                    }

                    if (existingItem == null)
                    {
                        _mediaItemRepository.Insert(mediaItem);
                        created++;
                    }
                    else
                    {
                        _mediaItemRepository.Update(mediaItem);
                        updated++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to enrich media file '{0}'", match.MediaFile.Path);
                }
            }

            _logger.Info("Enrichment complete: {0} created, {1} updated, {2} total files processed", created, updated, matches.Count);
        }

        private void PopulateFromRadarr(MediaItem item, RadarrMovie movie)
        {
            item.ExternalId = movie.Id;
            item.Title = movie.Title;
            item.Year = movie.Year;
            item.Monitored = movie.Monitored;
            item.Tags = movie.Tags != null ? string.Join(",", movie.Tags) : null;

            if (movie.QualityProfile != null)
            {
                var profileName = _qualityProfileCache.GetProfileName(MediaSource.Radarr, movie.QualityProfile.Id);
                item.QualityProfile = profileName ?? $"Profile {movie.QualityProfile.Id}";
            }
        }

        private void PopulateFromSonarr(MediaItem item, SonarrSeries series, SonarrEpisodeFile episodeFile)
        {
            item.ExternalId = episodeFile.Id;
            item.Title = series.Title;
            item.SeriesTitle = series.Title;
            item.Year = series.Year;
            item.SeasonNumber = episodeFile.SeasonNumber;
            item.Monitored = series.Monitored;
            item.Tags = series.Tags != null ? string.Join(",", series.Tags) : null;

            var profileName = _qualityProfileCache.GetProfileName(MediaSource.Sonarr, series.QualityProfileId);
            item.QualityProfile = profileName ?? $"Profile {series.QualityProfileId}";
        }
    }
}
