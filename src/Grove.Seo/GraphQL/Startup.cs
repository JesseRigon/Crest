using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Modules;
using Grove.Display.Resources.Management;
using Grove.Seo.Models;

namespace Grove.Seo.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MetaEntry, MetaEntryQueryObjectType>();
        services.AddObjectGraphType<SeoMetaFieldSet, SeoMetaQueryObjectType>();
    }
}
