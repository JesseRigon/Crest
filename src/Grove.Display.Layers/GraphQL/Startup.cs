using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Protocols.GraphQL;
using Grove.Modules;

namespace Grove.Display.Layers.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SiteLayersQuery>();
        services.AddTransient<LayerQueryObjectType>();
    }
}
