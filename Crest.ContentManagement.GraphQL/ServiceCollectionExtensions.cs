using Microsoft.Extensions.DependencyInjection;
using Crest.Apis.GraphQL;
using Crest.ContentManagement.GraphQL.Options;
using Crest.ContentManagement.GraphQL.Queries;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.ContentManagement.Records;
using Crest.ContentTypes.Events;
using Crest.Security.Permissions;
using YesSql.Indexes;

namespace Crest.ContentManagement.GraphQL;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentGraphQL(this IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, ContentItemQuery>();
        services.AddSingleton<ISchemaBuilder, ContentTypeQuery>();
        services.AddTransient<ContentItemInterface>();

        services.AddTransient<ContentItemType>();

        services.AddPermissionProvider<Permissions>();

        services.AddScoped<IContentTypeBuilder, TypedContentTypeBuilder>();
        services.AddScoped<IContentTypeBuilder, DynamicContentTypeQueryBuilder>();

        services.AddOptions<GraphQLContentOptions>();
        services.AddGraphQLFilterType<ContentItem, ContentItemFilters>();
        services.AddWhereInputIndexPropertyProvider<ContentItemIndex>();

        return services;
    }

    public static IServiceCollection AddContentFieldsInputGraphQL(this IServiceCollection services)
    {
        services.AddScoped<DynamicContentFieldsIndexAliasProvider>()
            .AddScoped<IIndexAliasProvider>(sp => sp.GetService<DynamicContentFieldsIndexAliasProvider>())
            .AddScoped<IContentDefinitionEventHandler>(sp => sp.GetService<DynamicContentFieldsIndexAliasProvider>());

        services.AddSingleton<IIndexPropertyProvider, DefaultDynamicIndexPropertyProvider>();
        services.AddScoped<IContentTypeBuilder, DynamicContentTypeWhereInputBuilder>();

        return services;
    }

    public static void AddWhereInputIndexPropertyProvider<TIndexType>(this IServiceCollection services)
        where TIndexType : MapIndex
    {
        services.AddSingleton<IIndexPropertyProvider, IndexPropertyProvider<TIndexType>>();
    }

    /// <summary>
    /// Registers a type providing custom filters for content item filters.
    /// </summary>
    /// <typeparam name="TObjectTypeToFilter"></typeparam>
    /// <typeparam name="TFilterType"></typeparam>
    /// <param name="services"></param>
    public static void AddGraphQLFilterType<TObjectTypeToFilter, TFilterType>(this IServiceCollection services)
        where TObjectTypeToFilter : class
        where TFilterType : GraphQLFilter<TObjectTypeToFilter>
    {
        services.AddTransient<IGraphQLFilter<TObjectTypeToFilter>, TFilterType>();
    }
}
