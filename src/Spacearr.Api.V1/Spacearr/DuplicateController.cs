using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("duplicate")]
    public class DuplicateController : Controller
    {
        private readonly IDuplicateDetectionService _duplicateService;
        private readonly ISpacearrActionService _actionService;

        public DuplicateController(IDuplicateDetectionService duplicateService, ISpacearrActionService actionService)
        {
            _duplicateService = duplicateService;
            _actionService = actionService;
        }

        [HttpGet]
        public List<DuplicateResource> GetAll()
        {
            return _duplicateService.GetDuplicateGroups().ToResource();
        }

        [HttpPost("{groupId}/keep")]
        [Consumes("application/json")]
        public ActionResult<DuplicateResource> Keep(int groupId, [FromBody] KeepRequest request)
        {
            var group = _duplicateService.GetGroup(groupId);

            if (group == null)
            {
                return NotFound();
            }

            var toDelete = group.Items.Where(i => i.MediaItem.Id != request.KeepMediaItemId).ToList();

            foreach (var item in toDelete)
            {
                _actionService.DeleteMediaFile(item.MediaItem.Id, false);
            }

            return Ok(group.ToResource());
        }
    }
}
