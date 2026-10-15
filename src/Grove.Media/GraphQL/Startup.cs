using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Apis.GraphQL;
using Grove.Media.Fields;
using Grove.Modules;

namespace Grove.Media.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, MediaAssetQuery>();
        services.AddObjectGraphType<MediaField, MediaFieldQueryObjectType>();
        services.AddTransient<MediaAssetObjectType>();
    }
}
