using NLog;

namespace NzbDrone.Core.Spacearr.ArrIntegration
{
    public interface IArrConnectionTestService
    {
        ArrConnectionTestResult TestRadarrConnection(ArrConnectionSettings settings);
        ArrConnectionTestResult TestSonarrConnection(ArrConnectionSettings settings);
    }

    public class ArrConnectionTestService : IArrConnectionTestService
    {
        private readonly IRadarrApiClient _radarrApiClient;
        private readonly ISonarrApiClient _sonarrApiClient;
        private readonly Logger _logger;

        public ArrConnectionTestService(IRadarrApiClient radarrApiClient, ISonarrApiClient sonarrApiClient, Logger logger)
        {
            _radarrApiClient = radarrApiClient;
            _sonarrApiClient = sonarrApiClient;
            _logger = logger;
        }

        public ArrConnectionTestResult TestRadarrConnection(ArrConnectionSettings settings)
        {
            _logger.Info("Testing Radarr connection to {0}", settings.Url);
            return _radarrApiClient.TestConnection(settings);
        }

        public ArrConnectionTestResult TestSonarrConnection(ArrConnectionSettings settings)
        {
            _logger.Info("Testing Sonarr connection to {0}", settings.Url);
            return _sonarrApiClient.TestConnection(settings);
        }
    }
}
