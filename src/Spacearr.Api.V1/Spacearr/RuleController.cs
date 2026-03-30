using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr.Services;
using Spacearr.Http;
using Spacearr.Http.REST;
using Spacearr.Http.REST.Attributes;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("rule")]
    public class RuleController : RestController<RuleResource>
    {
        private readonly IRuleEvaluationService _ruleEvaluationService;

        public RuleController(IRuleEvaluationService ruleEvaluationService)
        {
            _ruleEvaluationService = ruleEvaluationService;
        }

        protected override RuleResource GetResourceById(int id)
        {
            return _ruleEvaluationService.GetRule(id).ToResource();
        }

        [HttpGet]
        public List<RuleResource> GetAll()
        {
            return _ruleEvaluationService.GetAllRules().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<RuleResource> Create([FromBody] RuleResource resource)
        {
            var rule = resource.ToModel();
            var created = _ruleEvaluationService.AddRule(rule);

            return Created(created.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<RuleResource> Update([FromBody] RuleResource resource)
        {
            var rule = resource.ToModel();
            _ruleEvaluationService.UpdateRule(rule);

            return Accepted(resource.Id);
        }

        [RestDeleteById]
        public void DeleteRule(int id)
        {
            _ruleEvaluationService.DeleteRule(id);
        }

        [HttpPost("{id}/evaluate")]
        public ActionResult<RuleEvaluationResultResource> Evaluate(int id)
        {
            var rule = _ruleEvaluationService.GetRule(id);

            if (rule == null)
            {
                return NotFound();
            }

            var result = _ruleEvaluationService.Evaluate(rule);

            return Ok(result.ToResource());
        }
    }
}
