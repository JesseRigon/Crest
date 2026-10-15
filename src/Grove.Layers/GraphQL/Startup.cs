using Microsoft.Extensions.DependencyInjection;
using Grove.Apis.GraphQL;
using Grove.Modules;

namespace Grove.Layers.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SiteLayersQuery>();
        services.AddTransient<LayerQueryObjectType>();
    }
}
