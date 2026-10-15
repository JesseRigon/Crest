using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.Modules;
using Grove.Spatial.Fields;

namespace Grove.Spatial.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IContentFieldProvider, GeoPointFieldProvider>();
        services.AddObjectGraphType<GeoPointField, GeoPointFieldQueryObjectType>();
    }
}
