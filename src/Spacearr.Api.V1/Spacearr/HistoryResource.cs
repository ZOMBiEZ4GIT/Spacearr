using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class HistoryResource : RestResource
    {
        public string ActionType { get; set; }
        public int? MediaItemId { get; set; }
        public string Title { get; set; }
        public string Details { get; set; }
        public string OldQuality { get; set; }
        public string NewQuality { get; set; }
        public long SpaceFreedBytes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class HistoryStatsResource
    {
        public long TotalSpaceSavedBytes { get; set; }
        public Dictionary<string, int> CountByActionType { get; set; }
    }

    public static class HistoryResourceMapper
    {
        public static HistoryResource ToResource(this ActionHistory model)
        {
            if (model == null)
            {
                return null;
            }

            return new HistoryResource
            {
                Id = model.Id,
                ActionType = model.ActionType.ToString(),
                MediaItemId = model.MediaItemId,
                Title = model.Title,
                Details = model.Details,
                OldQuality = model.OldQuality,
                NewQuality = model.NewQuality,
                SpaceFreedBytes = model.SpaceFreedBytes,
                Timestamp = model.Timestamp
            };
        }

        public static List<HistoryResource> ToResource(this IEnumerable<ActionHistory> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
