using System;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Spacearr.ArrIntegration;
using Spacearr.Http;

namespace Spacearr.Api.V1.Config
{
    [V3ApiController("config/arrconnection")]
    public class ArrConnectionConfigController : ConfigController<ArrConnectionConfigResource>
    {
        private readonly IRadarrApiClient _radarrApiClient;
        private readonly ISonarrApiClient _sonarrApiClient;

        public ArrConnectionConfigController(
            IConfigService configService,
            IRadarrApiClient radarrApiClient,
            ISonarrApiClient sonarrApiClient)
            : base(configService)
        {
            _radarrApiClient = radarrApiClient;
            _sonarrApiClient = sonarrApiClient;
        }

        [HttpPost("test")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<ArrConnectionTestResponse> TestConnection([FromBody] ArrConnectionTestRequest request)
        {
            var response = new ArrConnectionTestResponse();

            if (request.TestRadarr)
            {
                var radarrSettings = new ArrConnectionSettings
                {
                    Url = request.RadarrUrl ?? _configService.RadarrUrl,
                    ApiKey = request.RadarrApiKey ?? _configService.RadarrApiKey,
                    Enabled = true
                };

                try
                {
                    var result = _radarrApiClient.TestConnection(radarrSettings);
                    response.RadarrSuccess = result.Success;
                    response.RadarrError = result.ErrorMessage;
                    response.RadarrVersion = result.Version;
                }
                catch (Exception ex)
                {
                    response.RadarrSuccess = false;
                    response.RadarrError = ex.Message;
                }
            }

            if (request.TestSonarr)
            {
                var sonarrSettings = new ArrConnectionSettings
                {
                    Url = request.SonarrUrl ?? _configService.SonarrUrl,
                    ApiKey = request.SonarrApiKey ?? _configService.SonarrApiKey,
                    Enabled = true
                };

                try
                {
                    var result = _sonarrApiClient.TestConnection(sonarrSettings);
                    response.SonarrSuccess = result.Success;
                    response.SonarrError = result.ErrorMessage;
                    response.SonarrVersion = result.Version;
                }
                catch (Exception ex)
                {
                    response.SonarrSuccess = false;
                    response.SonarrError = ex.Message;
                }
            }

            return Ok(response);
        }

        protected override ArrConnectionConfigResource ToResource(IConfigService model)
        {
            return ArrConnectionConfigResourceMapper.ToResource(model);
        }
    }

    public class ArrConnectionTestRequest
    {
        public bool TestRadarr { get; set; }
        public string RadarrUrl { get; set; }
        public string RadarrApiKey { get; set; }
        public bool TestSonarr { get; set; }
        public string SonarrUrl { get; set; }
        public string SonarrApiKey { get; set; }
    }

    public class ArrConnectionTestResponse
    {
        public bool RadarrSuccess { get; set; }
        public string RadarrError { get; set; }
        public string RadarrVersion { get; set; }
        public bool SonarrSuccess { get; set; }
        public string SonarrError { get; set; }
        public string SonarrVersion { get; set; }
    }
}
