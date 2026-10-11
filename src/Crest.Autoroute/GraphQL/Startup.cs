using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.Autoroute.Indexes;
using Crest.Autoroute.Models;
using Crest.ContentManagement.GraphQL;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.Modules;

namespace Crest.Autoroute.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
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
