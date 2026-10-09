using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentManagement.GraphQL;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Lists.Indexes;
using Crest.Lists.Models;
using Crest.Modules;

namespace Crest.Lists.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
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
