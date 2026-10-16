using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Routes.Auto.Indexes;
using Grove.Routes.Auto.Models;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Modules;

namespace Grove.Routes.Auto.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInputObjectGraphType<AutorouteFieldSet, AutorouteInputObjectType>();
        services.AddObjectGraphType<AutorouteFieldSet, AutorouteQueryObjectType>();
        services.AddTransient<IIndexAliasProvider, AutorouteIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<AutorouteIndex>();
    }
}
