using System;
using NLog;

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
        private readonly Logger _logger;

        public SpacearrActionService(
            IMediaItemRepository mediaItemRepository,
            IMediaFileRepository mediaFileRepository,
            IActionHistoryRepository actionHistoryRepository,
            Logger logger)
        {
            _mediaItemRepository = mediaItemRepository;
            _mediaFileRepository = mediaFileRepository;
            _actionHistoryRepository = actionHistoryRepository;
            _logger = logger;
        }

        public ActionHistory DeleteMediaFile(int mediaItemId, bool unmonitor)
        {
            var item = _mediaItemRepository.Get(mediaItemId);
            var file = _mediaFileRepository.Find(item.MediaFileId);
            var sizeFreed = file?.SizeBytes ?? 0;

            if (file != null)
            {
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
    }
}
