using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Modules;
using Grove.Data.Types.Spatial.Fields;

namespace Grove.Data.Types.Spatial.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IContentFieldProvider, GeoPointFieldProvider>();
        services.AddObjectGraphType<GeoPointField, GeoPointFieldQueryObjectType>();
    }
}
