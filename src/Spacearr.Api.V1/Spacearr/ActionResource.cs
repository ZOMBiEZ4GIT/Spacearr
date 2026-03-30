using System;
using NzbDrone.Core.Spacearr;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class ActionResource : RestResource
    {
        public string ActionType { get; set; }
        public int? MediaItemId { get; set; }
        public string Title { get; set; }
        public string Details { get; set; }
        public long SpaceFreedBytes { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DeleteRequest
    {
        public int MediaItemId { get; set; }
        public bool Unmonitor { get; set; }
    }

    public class SearchRequest
    {
        public int MediaItemId { get; set; }
    }

    public class QualitySwapRequest
    {
        public int MediaItemId { get; set; }
        public int NewQualityProfileId { get; set; }
    }

    public static class ActionResourceMapper
    {
        public static ActionResource ToResource(this ActionHistory model)
        {
            if (model == null)
            {
                return null;
            }

            return new ActionResource
            {
                Id = model.Id,
                ActionType = model.ActionType.ToString(),
                MediaItemId = model.MediaItemId,
                Title = model.Title,
                Details = model.Details,
                SpaceFreedBytes = model.SpaceFreedBytes,
                Timestamp = model.Timestamp
            };
        }
    }
}
