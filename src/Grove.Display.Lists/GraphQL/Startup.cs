using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Display.Lists.Indexes;
using Grove.Display.Lists.Models;
using Grove.Modules;

namespace Grove.Display.Lists.GraphQL;

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
