using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.Apis.GraphQL;
using Crest.Media.Fields;
using Crest.Modules;

namespace Crest.Media.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, MediaAssetQuery>();
        services.AddObjectGraphType<MediaField, MediaFieldQueryObjectType>();
        services.AddTransient<MediaAssetObjectType>();
    }
}
