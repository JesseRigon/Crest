using Microsoft.Extensions.DependencyInjection;
using Crest.Alias.Indexes;
using Crest.Alias.Models;
using Crest.Apis;
using Crest.ContentManagement.GraphQL;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.Modules;

namespace Crest.Alias.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<AliasPart, AliasQueryObjectType>();
        services.AddInputObjectGraphType<AliasPart, AliasInputObjectType>();
        services.AddTransient<IIndexAliasProvider, AliasPartIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<AliasPartIndex>();
    }
}
