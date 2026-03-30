using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Spacearr;
using Spacearr.Http;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("history")]
    public class SpacearrHistoryController : Controller
    {
        private readonly IActionHistoryRepository _actionHistoryRepository;

        public SpacearrHistoryController(IActionHistoryRepository actionHistoryRepository)
        {
            _actionHistoryRepository = actionHistoryRepository;
        }

        [HttpGet]
        public List<HistoryResource> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var all = _actionHistoryRepository.All()
                .OrderByDescending(x => x.Timestamp)
                .ToList();

            var skip = (page - 1) * pageSize;

            return all
                .Skip(skip)
                .Take(pageSize)
                .ToList()
                .ToResource();
        }

        [HttpGet("stats")]
        public HistoryStatsResource GetStats()
        {
            var all = _actionHistoryRepository.All().ToList();

            return new HistoryStatsResource
            {
                TotalSpaceSavedBytes = all.Sum(x => x.SpaceFreedBytes),
                CountByActionType = all.GroupBy(x => x.ActionType.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }
}
