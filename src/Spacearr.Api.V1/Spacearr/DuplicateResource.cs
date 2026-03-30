using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class DuplicateResource : RestResource
    {
        public int GroupId { get; set; }
        public string Title { get; set; }
        public List<DuplicateItemResource> Items { get; set; }
    }

    public class DuplicateItemResource
    {
        public int MediaItemId { get; set; }
        public string FilePath { get; set; }
        public long SizeBytes { get; set; }
        public string Codec { get; set; }
        public string Resolution { get; set; }
        public string QualityProfile { get; set; }
    }

    public class KeepRequest
    {
        public int KeepMediaItemId { get; set; }
    }

    public static class DuplicateResourceMapper
    {
        public static DuplicateResource ToResource(this DuplicateGroup model)
        {
            if (model == null)
            {
                return null;
            }

            return new DuplicateResource
            {
                Id = model.GroupId,
                GroupId = model.GroupId,
                Title = model.Title,
                Items = model.Items?.Select(i => new DuplicateItemResource
                {
                    MediaItemId = i.MediaItem.Id,
                    FilePath = i.MediaFile?.Path,
                    SizeBytes = i.MediaFile?.SizeBytes ?? 0,
                    Codec = i.MediaFile?.Codec,
                    Resolution = i.MediaFile?.Resolution,
                    QualityProfile = i.MediaItem.QualityProfile
                }).ToList()
            };
        }

        public static List<DuplicateResource> ToResource(this IEnumerable<DuplicateGroup> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
