using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("library")]
    public class LibraryController : RestController<LibraryResource>
    {
        private readonly ILibraryService _libraryService;

        public LibraryController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        protected override LibraryResource GetResourceById(int id)
        {
            var item = _libraryService.Get(id);

            if (item == null)
            {
                throw new NotFoundException("Media item not found");
            }

            return item.ToResource();
        }

        [HttpGet]
        public List<LibraryResource> GetAll(
            [FromQuery] string source = null,
            [FromQuery] long? minSize = null,
            [FromQuery] string sortBy = null,
            [FromQuery] bool ascending = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            MediaSource? sourceFilter = null;

            if (!string.IsNullOrEmpty(source) && Enum.TryParse<MediaSource>(source, true, out var parsed))
            {
                sourceFilter = parsed;
            }

            var items = _libraryService.GetFiltered(sourceFilter, minSize, sortBy, ascending);

            var skip = (page - 1) * pageSize;
            var paged = items.GetRange(
                Math.Min(skip, items.Count),
                Math.Min(pageSize, Math.Max(0, items.Count - skip)));

            return paged.ToResource();
        }

        [HttpGet("stats")]
        public LibraryStatsResource GetStats()
        {
            return _libraryService.GetStats().ToResource();
        }
    }
}
