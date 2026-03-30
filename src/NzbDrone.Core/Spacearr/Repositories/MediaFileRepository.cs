using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Spacearr
{
    public interface IMediaFileRepository : IBasicRepository<MediaFile>
    {
        MediaFile FindByPath(string path);
        List<MediaFile> FindByLibraryPath(string libraryPath);
    }

    public class MediaFileRepository : BasicRepository<MediaFile>, IMediaFileRepository
    {
        public MediaFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public MediaFile FindByPath(string path)
        {
            return Query(x => x.Path == path).SingleOrDefault();
        }

        public List<MediaFile> FindByLibraryPath(string libraryPath)
        {
            return Query(x => x.LibraryPath == libraryPath);
        }
    }
}
