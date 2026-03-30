using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Spacearr
{
    public interface IMediaItemRepository : IBasicRepository<MediaItem>
    {
        MediaItem FindByMediaFileId(int mediaFileId);
        MediaItem FindByExternalId(MediaSource source, int externalId);
        List<MediaItem> GetBySource(MediaSource source);
    }

    public class MediaItemRepository : BasicRepository<MediaItem>, IMediaItemRepository
    {
        public MediaItemRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public MediaItem FindByMediaFileId(int mediaFileId)
        {
            return Query(x => x.MediaFileId == mediaFileId).SingleOrDefault();
        }

        public MediaItem FindByExternalId(MediaSource source, int externalId)
        {
            return Query(x => x.Source == source && x.ExternalId == externalId).SingleOrDefault();
        }

        public List<MediaItem> GetBySource(MediaSource source)
        {
            return Query(x => x.Source == source);
        }
    }
}
