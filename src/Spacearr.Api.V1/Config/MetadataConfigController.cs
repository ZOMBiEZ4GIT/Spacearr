using NzbDrone.Core.Configuration;
using Spacearr.Http;

namespace Spacearr.Api.V1.Config
{
    [V3ApiController("config/metadata")]
    public class MetadataConfigController : ConfigController<MetadataConfigResource>
    {
        public MetadataConfigController(IConfigService configService)
            : base(configService)
        {
        }

        protected override MetadataConfigResource ToResource(IConfigService model)
        {
            return MetadataConfigResourceMapper.ToResource(model);
        }
    }
}
