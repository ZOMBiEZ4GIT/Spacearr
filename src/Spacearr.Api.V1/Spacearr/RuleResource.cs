using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class RuleResource : RestResource
    {
        public string Name { get; set; }
        public string FilterCriteria { get; set; }
        public string ActionType { get; set; }
        public string TargetQuality { get; set; }
        public bool Active { get; set; }
        public DateTime? LastEvaluated { get; set; }
    }

    public class RuleEvaluationResultResource
    {
        public int RuleId { get; set; }
        public int MatchedItems { get; set; }
        public string Summary { get; set; }
    }

    public static class RuleResourceMapper
    {
        public static RuleResource ToResource(this Rule model)
        {
            if (model == null)
            {
                return null;
            }

            return new RuleResource
            {
                Id = model.Id,
                Name = model.Name,
                FilterCriteria = model.FilterCriteria,
                ActionType = model.ActionType.ToString(),
                TargetQuality = model.TargetQuality,
                Active = model.Active,
                LastEvaluated = model.LastEvaluated
            };
        }

        public static Rule ToModel(this RuleResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            Enum.TryParse<RuleActionType>(resource.ActionType, true, out var actionType);

            return new Rule
            {
                Id = resource.Id,
                Name = resource.Name,
                FilterCriteria = resource.FilterCriteria,
                ActionType = actionType,
                TargetQuality = resource.TargetQuality,
                Active = resource.Active,
                LastEvaluated = resource.LastEvaluated
            };
        }

        public static List<RuleResource> ToResource(this IEnumerable<Rule> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static RuleEvaluationResultResource ToResource(this RuleEvaluationResult model)
        {
            if (model == null)
            {
                return null;
            }

            return new RuleEvaluationResultResource
            {
                RuleId = model.RuleId,
                MatchedItems = model.MatchedItems,
                Summary = model.Summary
            };
        }
    }
}
