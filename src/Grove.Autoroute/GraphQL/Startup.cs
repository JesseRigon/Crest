using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Autoroute.Indexes;
using Grove.Autoroute.Models;
using Grove.ContentManagement.GraphQL;
using Grove.ContentManagement.GraphQL.Queries;
using Grove.Modules;

namespace Grove.Autoroute.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInputObjectGraphType<AutoroutePart, AutorouteInputObjectType>();
        services.AddObjectGraphType<AutoroutePart, AutorouteQueryObjectType>();
        services.AddTransient<IIndexAliasProvider, AutoroutePartIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<AutoroutePartIndex>();
    }
}
