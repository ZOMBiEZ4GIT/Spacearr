using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class ScanResource : RestResource
    {
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; }
        public int FilesScanned { get; set; }
        public int FilesAdded { get; set; }
        public int FilesRemoved { get; set; }
        public string ErrorMessage { get; set; }
    }

    public static class ScanResourceMapper
    {
        public static ScanResource ToResource(this ScanJob model)
        {
            if (model == null)
            {
                return null;
            }

            return new ScanResource
            {
                Id = model.Id,
                StartedAt = model.StartedAt,
                CompletedAt = model.CompletedAt,
                Status = model.Status.ToString(),
                FilesScanned = model.FilesScanned,
                FilesAdded = model.FilesAdded,
                FilesRemoved = model.FilesRemoved,
                ErrorMessage = model.ErrorMessage
            };
        }

        public static List<ScanResource> ToResource(this IEnumerable<ScanJob> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
