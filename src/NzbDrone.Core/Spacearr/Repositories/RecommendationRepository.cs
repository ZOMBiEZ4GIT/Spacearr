using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Spacearr
{
    public interface IRecommendationRepository : IBasicRepository<Recommendation>
    {
        List<Recommendation> GetActive();
        List<Recommendation> GetByMediaItemId(int mediaItemId);
        void DismissAll();
    }

    public class RecommendationRepository : BasicRepository<Recommendation>, IRecommendationRepository
    {
        public RecommendationRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<Recommendation> GetActive()
        {
            return Query(x => x.Dismissed == false);
        }

        public List<Recommendation> GetByMediaItemId(int mediaItemId)
        {
            return Query(x => x.MediaItemId == mediaItemId);
        }

        public void DismissAll()
        {
            var active = GetActive();

            foreach (var recommendation in active)
            {
                recommendation.Dismissed = true;
            }

            UpdateMany(active);
        }
    }
}
