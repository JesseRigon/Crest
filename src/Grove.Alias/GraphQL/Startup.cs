using Microsoft.Extensions.DependencyInjection;
using Grove.Alias.Indexes;
using Grove.Alias.Models;
using Grove.Apis;
using Grove.ContentManagement.GraphQL;
using Grove.ContentManagement.GraphQL.Queries;
using Grove.Modules;

namespace Grove.Alias.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
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
