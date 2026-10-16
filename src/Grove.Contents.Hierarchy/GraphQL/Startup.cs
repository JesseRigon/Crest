using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Hierarchy.Indexes;
using Grove.Contents.Hierarchy.Models;
using Grove.Modules;

namespace Grove.Contents.Hierarchy.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddInputObjectGraphType<ParentContentItemFieldSet, ContainedInputObjectType>();
        services.AddObjectGraphType<ParentContentItemFieldSet, ContainedQueryObjectType>();
        services.AddObjectGraphType<ChildContentItemsFieldSet, ListQueryObjectType>();
        services.AddTransient<IIndexAliasProvider, ParentContentItemIndexAliasProvider>();
        services.AddWhereInputIndexPropertyProvider<ParentContentItemIndex>();

        services.AddScoped<IContentTypeBuilder, ParentContentItemFieldSetContentTypeBuilder>();
        services.AddTransient<IContentItemTypeInitializer, ParentContentItemFieldSetContentItemTypeInitializer>();
    }
}
