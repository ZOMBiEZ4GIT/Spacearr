using NLog;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Spacearr.Scanner
{
    public class ScanLibraryCommandHandler : IExecute<ScanLibraryCommand>
    {
        private readonly IFileScannerService _fileScannerService;
        private readonly Logger _logger;

        public ScanLibraryCommandHandler(IFileScannerService fileScannerService, Logger logger)
        {
            _fileScannerService = fileScannerService;
            _logger = logger;
        }

        public void Execute(ScanLibraryCommand message)
        {
            _logger.Info("Starting Spacearr library scan");
            _fileScannerService.Scan();
            _logger.Info("Spacearr library scan completed");
        }
    }
}
