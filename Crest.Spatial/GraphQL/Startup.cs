using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Modules;
using Crest.Spatial.Fields;

namespace Crest.Spatial.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IContentFieldProvider, GeoPointFieldProvider>();
        services.AddObjectGraphType<GeoPointField, GeoPointFieldQueryObjectType>();
    }
}
