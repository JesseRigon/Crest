using Microsoft.Extensions.DependencyInjection;
using Crest.Apis.GraphQL;
using Crest.Modules;

namespace Crest.Layers.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SiteLayersQuery>();
        services.AddTransient<LayerQueryObjectType>();
        services.AddTransient<LayerWidgetQueryObjectType>();
    }
}
