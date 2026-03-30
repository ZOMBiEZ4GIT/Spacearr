using System;
using System.Collections.Generic;
using NLog;

namespace NzbDrone.Core.Spacearr.Services
{
    public class RuleEvaluationResult
    {
        public int RuleId { get; set; }
        public int MatchedItems { get; set; }
        public string Summary { get; set; }
    }

    public interface IRuleEvaluationService
    {
        RuleEvaluationResult Evaluate(Rule rule);
        List<Rule> GetAllRules();
        Rule GetRule(int id);
        Rule AddRule(Rule rule);
        Rule UpdateRule(Rule rule);
        void DeleteRule(int id);
    }

    public class RuleEvaluationService : IRuleEvaluationService
    {
        private readonly IRuleRepository _ruleRepository;
        private readonly ILibraryService _libraryService;
        private readonly Logger _logger;

        public RuleEvaluationService(IRuleRepository ruleRepository, ILibraryService libraryService, Logger logger)
        {
            _ruleRepository = ruleRepository;
            _libraryService = libraryService;
            _logger = logger;
        }

        public RuleEvaluationResult Evaluate(Rule rule)
        {
            _logger.Info("Evaluating rule: {0}", rule.Name);

            rule.LastEvaluated = DateTime.UtcNow;
            _ruleRepository.Update(rule);

            var items = _libraryService.GetAll();

            return new RuleEvaluationResult
            {
                RuleId = rule.Id,
                MatchedItems = 0,
                Summary = $"Rule '{rule.Name}' evaluated against {items.Count} items"
            };
        }

        public List<Rule> GetAllRules()
        {
            return new List<Rule>(_ruleRepository.All());
        }

        public Rule GetRule(int id)
        {
            return _ruleRepository.Get(id);
        }

        public Rule AddRule(Rule rule)
        {
            return _ruleRepository.Insert(rule);
        }

        public Rule UpdateRule(Rule rule)
        {
            return _ruleRepository.Update(rule);
        }

        public void DeleteRule(int id)
        {
            _ruleRepository.Delete(id);
        }
    }
}
