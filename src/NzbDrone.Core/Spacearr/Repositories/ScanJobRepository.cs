using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Spacearr
{
    public interface IScanJobRepository : IBasicRepository<ScanJob>
    {
        ScanJob GetLatest();
        ScanJob GetRunning();
    }

    public class ScanJobRepository : BasicRepository<ScanJob>, IScanJobRepository
    {
        public ScanJobRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public ScanJob GetLatest()
        {
            return All().OrderByDescending(x => x.StartedAt).FirstOrDefault();
        }

        public ScanJob GetRunning()
        {
            return Query(x => x.Status == ScanStatus.Running).SingleOrDefault();
        }
    }
}
