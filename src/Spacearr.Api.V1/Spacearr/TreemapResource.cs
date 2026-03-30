using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    public class TreemapResource : RestResource
    {
        public string Title { get; set; }
        public long SizeBytes { get; set; }
        public long BitrateBps { get; set; }
        public string Codec { get; set; }
        public string Resolution { get; set; }
        public string QualityProfile { get; set; }
        public string Source { get; set; }
        public string ParentGroup { get; set; }
    }

    public static class TreemapResourceMapper
    {
        public static TreemapResource ToTreemapResource(this LibraryItem model)
        {
            if (model == null)
            {
                return null;
            }

            return new TreemapResource
            {
                Id = model.MediaItem.Id,
                Title = model.MediaItem.Title,
                SizeBytes = model.MediaFile?.SizeBytes ?? 0,
                BitrateBps = model.MediaFile?.BitrateBps ?? 0,
                Codec = model.MediaFile?.Codec,
                Resolution = model.MediaFile?.Resolution,
                QualityProfile = model.MediaItem.QualityProfile,
                Source = model.MediaItem.Source.ToString(),
                ParentGroup = model.MediaItem.SeriesTitle
            };
        }

        public static List<TreemapResource> ToTreemapResource(this IEnumerable<LibraryItem> models)
        {
            return models.Select(ToTreemapResource).ToList();
        }
    }
}
