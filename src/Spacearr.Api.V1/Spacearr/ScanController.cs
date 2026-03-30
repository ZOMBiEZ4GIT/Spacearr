using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Spacearr;
using NzbDrone.Core.Spacearr.Scanner;
using Spacearr.Http;

namespace Spacearr.Api.V1.Spacearr
{
    [V3ApiController("scan")]
    public class ScanController : Controller
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IScanJobRepository _scanJobRepository;

        public ScanController(IManageCommandQueue commandQueueManager, IScanJobRepository scanJobRepository)
        {
            _commandQueueManager = commandQueueManager;
            _scanJobRepository = scanJobRepository;
        }

        [HttpPost]
        public ActionResult<ScanResource> TriggerScan()
        {
            var command = new ScanLibraryCommand();
            _commandQueueManager.Push(command, CommandPriority.Normal, CommandTrigger.Manual);

            var latest = _scanJobRepository.GetLatest();

            return Created("", latest.ToResource());
        }

        [HttpGet("status")]
        public ActionResult<ScanResource> GetStatus()
        {
            var running = _scanJobRepository.GetRunning();

            if (running != null)
            {
                return Ok(running.ToResource());
            }

            var latest = _scanJobRepository.GetLatest();

            if (latest == null)
            {
                return NotFound();
            }

            return Ok(latest.ToResource());
        }

        [HttpGet("history")]
        public List<ScanResource> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var all = _scanJobRepository.All()
                .OrderByDescending(x => x.StartedAt)
                .ToList();

            var skip = (page - 1) * pageSize;

            return all
                .Skip(skip)
                .Take(pageSize)
                .ToList()
                .ToResource();
        }
    }
}
