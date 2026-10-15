using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Alias.Indexes;
using Grove.Contents.Alias.Models;
using Grove.Apis;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Modules;

namespace Grove.Contents.Alias.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
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
