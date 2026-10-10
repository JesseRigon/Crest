using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.Modules;
using Crest.ResourceManagement;
using Crest.Seo.Models;

namespace Crest.Seo.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MetaEntry, MetaEntryQueryObjectType>();
        services.AddObjectGraphType<SeoMetaPart, SeoMetaQueryObjectType>();
    }
}
