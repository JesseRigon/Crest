using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Modules;
using Grove.ResourceManagement;
using Grove.Seo.Models;

namespace Grove.Seo.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MetaEntry, MetaEntryQueryObjectType>();
        services.AddObjectGraphType<SeoMetaPart, SeoMetaQueryObjectType>();
    }
}
