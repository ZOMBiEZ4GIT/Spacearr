using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("recommendation")]
    public class RecommendationController : RestController<RecommendationResource>
    {
        private readonly IRecommendationRepository _recommendationRepository;
        private readonly ISpacearrActionService _actionService;

        public RecommendationController(
            IRecommendationRepository recommendationRepository,
            ISpacearrActionService actionService)
        {
            _recommendationRepository = recommendationRepository;
            _actionService = actionService;
        }

        protected override RecommendationResource GetResourceById(int id)
        {
            return _recommendationRepository.Get(id).ToResource();
        }

        [HttpGet]
        public List<RecommendationResource> GetActive()
        {
            return _recommendationRepository.GetActive()
                .OrderByDescending(x => x.EstimatedSavingsBytes)
                .ToList()
                .ToResource();
        }

        [HttpPost("{id}/accept")]
        public ActionResult<ActionResource> Accept(int id)
        {
            var recommendation = _recommendationRepository.Get(id);

            if (recommendation == null)
            {
                return NotFound();
            }

            var result = _actionService.QualitySwap(recommendation.MediaItemId, 0);

            recommendation.Dismissed = true;
            _recommendationRepository.Update(recommendation);

            return Ok(ActionResourceMapper.ToResource(result));
        }

        [HttpPost("{id}/dismiss")]
        public ActionResult<RecommendationResource> Dismiss(int id)
        {
            var recommendation = _recommendationRepository.Get(id);

            if (recommendation == null)
            {
                return NotFound();
            }

            recommendation.Dismissed = true;
            _recommendationRepository.Update(recommendation);

            return Ok(recommendation.ToResource());
        }

        [HttpPost("bulkdismiss")]
        [Consumes("application/json")]
        public ActionResult<List<RecommendationResource>> BulkDismiss([FromBody] BulkDismissRequest request)
        {
            if (request?.Ids == null || !request.Ids.Any())
            {
                return BadRequest("No IDs provided");
            }

            var recommendations = _recommendationRepository.Get(request.Ids).ToList();

            foreach (var rec in recommendations)
            {
                rec.Dismissed = true;
            }

            _recommendationRepository.UpdateMany(recommendations);

            return Ok(recommendations.ToResource());
        }
    }
}
