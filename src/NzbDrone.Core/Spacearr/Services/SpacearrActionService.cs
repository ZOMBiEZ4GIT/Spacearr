using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Spacearr.ArrIntegration;

namespace NzbDrone.Core.Spacearr.Services
{
    public interface ISpacearrActionService
    {
        ActionHistory DeleteMediaFile(int mediaItemId, bool unmonitor);
        ActionHistory TriggerSearch(int mediaItemId);
        ActionHistory QualitySwap(int mediaItemId, int newQualityProfileId);
    }

    public class SpacearrActionService : ISpacearrActionService
    {
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IMediaFileRepository _mediaFileRepository;
        private readonly IActionHistoryRepository _actionHistoryRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly IRadarrApiClient _radarrApiClient;
        private readonly ISonarrApiClient _sonarrApiClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public SpacearrActionService(
            IMediaItemRepository mediaItemRepository,
            IMediaFileRepository mediaFileRepository,
            IActionHistoryRepository actionHistoryRepository,
            IDiskProvider diskProvider,
            IRadarrApiClient radarrApiClient,
            ISonarrApiClient sonarrApiClient,
            IConfigService configService,
            Logger logger)
        {
            _mediaItemRepository = mediaItemRepository;
            _mediaFileRepository = mediaFileRepository;
            _actionHistoryRepository = actionHistoryRepository;
            _diskProvider = diskProvider;
            _radarrApiClient = radarrApiClient;
            _sonarrApiClient = sonarrApiClient;
            _configService = configService;
            _logger = logger;
        }

        public ActionHistory DeleteMediaFile(int mediaItemId, bool unmonitor)
        {
            var item = _mediaItemRepository.Get(mediaItemId);
            var file = _mediaFileRepository.Find(item.MediaFileId);
            var sizeFreed = file?.SizeBytes ?? 0;

            if (file != null)
            {
                // Delete the actual file from disk before removing the DB record
                try
                {
                    _diskProvider.DeleteFile(file.Path);
                    _logger.Info("Deleted file from disk: {0}", file.Path);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to delete file from disk: {0}", file.Path);
                    throw;
                }

                _mediaFileRepository.Delete(file);
            }

            if (unmonitor)
            {
                item.Monitored = false;
                _mediaItemRepository.Update(item);
            }

            var history = new ActionHistory
            {
                ActionType = ActionType.Delete,
                MediaItemId = mediaItemId,
                Title = item.Title,
                Details = unmonitor ? "Deleted and unmonitored" : "Deleted",
                SpaceFreedBytes = sizeFreed,
                Timestamp = DateTime.UtcNow
            };

            return _actionHistoryRepository.Insert(history);
        }

        public ActionHistory TriggerSearch(int mediaItemId)
        {
            var item = _mediaItemRepository.Get(mediaItemId);

            _logger.Info("Triggering search for {0} in {1}", item.Title, item.Source);

            try
            {
                switch (item.Source)
                {
                    case MediaSource.Radarr:
                        var radarrSettings = BuildRadarrSettings();
                        _radarrApiClient.TriggerMovieSearch(radarrSettings, item.ExternalId);
                        _logger.Info("Search triggered in Radarr for movie ID {0}", item.ExternalId);
                        break;

                    case MediaSource.Sonarr:
                        var sonarrSettings = BuildSonarrSettings();
                        _sonarrApiClient.TriggerEpisodeSearch(sonarrSettings, new List<int> { item.ExternalId });
                        _logger.Info("Search triggered in Sonarr for episode file ID {0}", item.ExternalId);
                        break;

                    default:
                        _logger.Warn("Cannot trigger search for item '{0}' with unknown source", item.Title);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to trigger search for '{0}' in {1}", item.Title, item.Source);
                throw;
            }

            var history = new ActionHistory
            {
                ActionType = ActionType.Search,
                MediaItemId = mediaItemId,
                Title = item.Title,
                Details = $"Search triggered in {item.Source}",
                SpaceFreedBytes = 0,
                Timestamp = DateTime.UtcNow
            };

            return _actionHistoryRepository.Insert(history);
        }

        public ActionHistory QualitySwap(int mediaItemId, int newQualityProfileId)
        {
            var item = _mediaItemRepository.Get(mediaItemId);
            var oldQuality = item.QualityProfile;

            _logger.Info("Quality swap for {0}: {1} -> profile {2}", item.Title, oldQuality, newQualityProfileId);

            try
            {
                switch (item.Source)
                {
                    case MediaSource.Radarr:
                        var radarrSettings = BuildRadarrSettings();
                        _radarrApiClient.UpdateQualityProfile(radarrSettings, item.ExternalId, newQualityProfileId);
                        _radarrApiClient.TriggerMovieSearch(radarrSettings, item.ExternalId);
                        _logger.Info("Quality profile updated and search triggered in Radarr for movie ID {0}", item.ExternalId);
                        break;

                    case MediaSource.Sonarr:
                        var sonarrSettings = BuildSonarrSettings();
                        _sonarrApiClient.UpdateSeriesQualityProfile(sonarrSettings, item.ExternalId, newQualityProfileId);
                        _sonarrApiClient.TriggerEpisodeSearch(sonarrSettings, new List<int> { item.ExternalId });
                        _logger.Info("Quality profile updated and search triggered in Sonarr for series ID {0}", item.ExternalId);
                        break;

                    default:
                        _logger.Warn("Cannot perform quality swap for item '{0}' with unknown source", item.Title);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to perform quality swap for '{0}' in {1}", item.Title, item.Source);
                throw;
            }

            var history = new ActionHistory
            {
                ActionType = ActionType.QualitySwap,
                MediaItemId = mediaItemId,
                Title = item.Title,
                OldQuality = oldQuality,
                NewQuality = newQualityProfileId.ToString(),
                Details = $"Quality profile changed from {oldQuality} to {newQualityProfileId}",
                SpaceFreedBytes = 0,
                Timestamp = DateTime.UtcNow
            };

            return _actionHistoryRepository.Insert(history);
        }

        private ArrConnectionSettings BuildRadarrSettings()
        {
            return new ArrConnectionSettings
            {
                Url = _configService.RadarrUrl,
                ApiKey = _configService.RadarrApiKey,
                Enabled = _configService.RadarrEnabled
            };
        }

        private ArrConnectionSettings BuildSonarrSettings()
        {
            return new ArrConnectionSettings
            {
                Url = _configService.SonarrUrl,
                ApiKey = _configService.SonarrApiKey,
                Enabled = _configService.SonarrEnabled
            };
        }
    }
}
