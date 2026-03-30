using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("action")]
    public class ActionController : Controller
    {
        private readonly ISpacearrActionService _actionService;

        public ActionController(ISpacearrActionService actionService)
        {
            _actionService = actionService;
        }

        [HttpPost("delete")]
        [Consumes("application/json")]
        public ActionResult<ActionResource> Delete([FromBody] DeleteRequest request)
        {
            if (request == null || request.MediaItemId <= 0)
            {
                return BadRequest("Invalid media item ID");
            }

            var result = _actionService.DeleteMediaFile(request.MediaItemId, request.Unmonitor);

            return Ok(ActionResourceMapper.ToResource(result));
        }

        [HttpPost("search")]
        [Consumes("application/json")]
        public ActionResult<ActionResource> Search([FromBody] SearchRequest request)
        {
            if (request == null || request.MediaItemId <= 0)
            {
                return BadRequest("Invalid media item ID");
            }

            var result = _actionService.TriggerSearch(request.MediaItemId);

            return Ok(ActionResourceMapper.ToResource(result));
        }

        [HttpPost("qualityswap")]
        [Consumes("application/json")]
        public ActionResult<ActionResource> QualitySwap([FromBody] QualitySwapRequest request)
        {
            if (request == null || request.MediaItemId <= 0)
            {
                return BadRequest("Invalid media item ID");
            }

            var result = _actionService.QualitySwap(request.MediaItemId, request.NewQualityProfileId);

            return Ok(ActionResourceMapper.ToResource(result));
        }
    }
}
