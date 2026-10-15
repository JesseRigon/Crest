using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentManagement.GraphQL;
using Grove.ContentManagement.GraphQL.Queries;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.Lists.Indexes;
using Grove.Lists.Models;
using Grove.Modules;

namespace Grove.Lists.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
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
