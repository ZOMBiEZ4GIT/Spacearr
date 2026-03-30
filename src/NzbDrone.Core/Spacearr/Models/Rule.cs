using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Spacearr
{
    public class Rule : ModelBase
    {
        public string Name { get; set; }
        public string FilterCriteria { get; set; }
        public RuleActionType ActionType { get; set; }
        public string TargetQuality { get; set; }
        public bool Active { get; set; }
        public DateTime? LastEvaluated { get; set; }
    }
}
