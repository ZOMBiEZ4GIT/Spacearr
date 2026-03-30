using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("treemap")]
    public class TreemapController : Controller
    {
        private readonly ILibraryService _libraryService;

        public TreemapController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        [HttpGet]
        public List<TreemapResource> GetTreemap(
            [FromQuery] string colorBy = "bitrate",
            [FromQuery] string source = "all",
            [FromQuery] long? minSize = null)
        {
            MediaSource? sourceFilter = null;

            if (!string.IsNullOrEmpty(source) && !source.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<MediaSource>(source, true, out var parsed))
                {
                    sourceFilter = parsed;
                }
            }

            var items = _libraryService.GetFiltered(sourceFilter, minSize, "size", false);

            return items.ToTreemapResource();
        }
    }
}
