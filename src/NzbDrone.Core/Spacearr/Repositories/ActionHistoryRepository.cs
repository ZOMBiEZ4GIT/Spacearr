using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Spacearr
{
    public interface IActionHistoryRepository : IBasicRepository<ActionHistory>
    {
        List<ActionHistory> GetRecent(int count);
        List<ActionHistory> GetByMediaItemId(int mediaItemId);
    }

    public class ActionHistoryRepository : BasicRepository<ActionHistory>, IActionHistoryRepository
    {
        public ActionHistoryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<ActionHistory> GetRecent(int count)
        {
            return All().OrderByDescending(x => x.Timestamp).Take(count).ToList();
        }

        public List<ActionHistory> GetByMediaItemId(int mediaItemId)
        {
            return Query(x => x.MediaItemId == mediaItemId);
        }
    }
}
