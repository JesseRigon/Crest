using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.ContentManagement.Cache;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Records;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Environment.Cache;

namespace Crest.ContentManagement;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentManagement(this IServiceCollection services)
    {
        services.AddScoped<ICacheContextProvider, ContentDefinitionCacheContextProvider>();
        services.TryAddScoped<IContentDefinitionManager, ContentDefinitionManager>();
        services.TryAddScoped<IContentDefinitionStore, DatabaseContentDefinitionStore>();
        services.TryAddScoped<IContentManager, DefaultContentManager>();
        services.TryAddScoped<IContentManagerSession, DefaultContentManagerSession>();
        services.AddIndexProvider<ContentItemIndexProvider>();
        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentHandler, UpdateContentsHandler>();
        services.AddScoped<IContentHandler, ContentPartHandlerCoordinator>();
        services.AddSingleton<ITypeActivatorFactory<ContentPart>, ContentPartFactory>();
        services.AddSingleton<ITypeActivatorFactory<ContentField>, ContentFieldFactory>();

        services.AddSingleton<IContentItemIdGenerator, DefaultContentItemIdGenerator>();
        services.AddScoped<IContentHandleManager, ContentHandleManager>();

        services.AddOptions<ContentOptions>();
        services.AddScoped<IContentPartHandlerResolver, ContentPartHandlerResolver>();
        services.AddScoped<IContentFieldHandlerResolver, ContentFieldHandlerResolver>();

        return services;
    }

    public static IServiceCollection AddFileContentDefinitionStore(this IServiceCollection services)
    {
        services.RemoveAll<IContentDefinitionStore>();
        services.AddScoped<IContentDefinitionStore, FileContentDefinitionStore>();
        return services;
    }
}
