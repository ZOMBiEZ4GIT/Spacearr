using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class RecommendationResource : RestResource
    {
        public int MediaItemId { get; set; }
        public string RecommendationType { get; set; }
        public long CurrentSizeBytes { get; set; }
        public long EstimatedNewSizeBytes { get; set; }
        public long EstimatedSavingsBytes { get; set; }
        public string SuggestedQuality { get; set; }
        public bool Dismissed { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BulkDismissRequest
    {
        public List<int> Ids { get; set; }
    }

    public static class RecommendationResourceMapper
    {
        public static RecommendationResource ToResource(this Recommendation model)
        {
            if (model == null)
            {
                return null;
            }

            return new RecommendationResource
            {
                Id = model.Id,
                MediaItemId = model.MediaItemId,
                RecommendationType = model.RecommendationType.ToString(),
                CurrentSizeBytes = model.CurrentSizeBytes,
                EstimatedNewSizeBytes = model.EstimatedNewSizeBytes,
                EstimatedSavingsBytes = model.EstimatedSavingsBytes,
                SuggestedQuality = model.SuggestedQuality,
                Dismissed = model.Dismissed,
                CreatedAt = model.CreatedAt
            };
        }

        public static List<RecommendationResource> ToResource(this IEnumerable<Recommendation> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
