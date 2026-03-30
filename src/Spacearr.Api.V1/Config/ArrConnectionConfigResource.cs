using NzbDrone.Core.Configuration;
using Spacearr.Http.REST;

namespace Spacearr.Api.V1.Config
{
    public class ArrConnectionConfigResource : RestResource
    {
        public string RadarrUrl { get; set; }
        public string RadarrApiKey { get; set; }
        public bool RadarrEnabled { get; set; }
        public string SonarrUrl { get; set; }
        public string SonarrApiKey { get; set; }
        public bool SonarrEnabled { get; set; }
    }

    public static class ArrConnectionConfigResourceMapper
    {
        public static ArrConnectionConfigResource ToResource(IConfigService model)
        {
            return new ArrConnectionConfigResource
            {
                RadarrUrl = model.RadarrUrl,
                RadarrApiKey = model.RadarrApiKey,
                RadarrEnabled = model.RadarrEnabled,
                SonarrUrl = model.SonarrUrl,
                SonarrApiKey = model.SonarrApiKey,
                SonarrEnabled = model.SonarrEnabled
            };
        }
    }
}
