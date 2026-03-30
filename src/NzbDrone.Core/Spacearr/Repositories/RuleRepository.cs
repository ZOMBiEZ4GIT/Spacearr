using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Spacearr
{
    public interface IRuleRepository : IBasicRepository<Rule>
    {
        List<Rule> GetActive();
    }

    public class RuleRepository : BasicRepository<Rule>, IRuleRepository
    {
        public RuleRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Rule> GetActive()
        {
            return Query(x => x.Active == true);
        }
    }
}
