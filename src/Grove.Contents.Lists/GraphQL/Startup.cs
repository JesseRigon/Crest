using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Lists.Indexes;
using Grove.Contents.Lists.Models;
using Grove.Modules;

namespace Grove.Contents.Lists.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInputObjectGraphType<ContainedPart, ContainedInputObjectType>();
        services.AddObjectGraphType<ContainedPart, ContainedQueryObjectType>();
        services.AddObjectGraphType<ListPart, ListQueryObjectType>();
        services.AddTransient<IIndexAliasProvider, ContainedPartIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<ContainedPartIndex>();

        services.AddScoped<IContentTypeBuilder, ContainedPartContentTypeBuilder>();
        services.AddTransient<IContentItemTypeInitializer, ContainedPartContentItemTypeInitializer>();
    }
}
