using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Contents.Management.Cache;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Records;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Environment.Cache;
using Grove.Validation;

namespace Grove.Contents.Management;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentManagement(this IServiceCollection services)
    {
        services.AddScoped<ICacheContextProvider, ContentDefinitionCacheContextProvider>();
        services.TryAddScoped<IContentDefinitionManager, ContentDefinitionManager>();
        services.TryAddScoped<IContentDefinitionStore, DatabaseContentDefinitionStore>();
        services.TryAddScoped<IContentManager, DefaultContentManager>();
        services.TryAddScoped<IContentLifecycle, ContentLifecycle>();
        services.TryAddScoped<IEmbeddedContentReader, EmbeddedContentReader>();
        services.TryAddScoped<IValidationSink, ValidationSink>();
        services.TryAddScoped<IContentManagerSession, DefaultContentManagerSession>();
        services.AddIndexProvider<ContentItemIndexProvider>();
        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentHandler, UpdateContentsHandler>();
        services.AddScoped<IContentHandler, ContentFieldSetHandlerCoordinator>();
        services.AddSingleton<ITypeActivatorFactory<ContentFieldSet>, ContentFieldSetFactory>();
        services.AddSingleton<ITypeActivatorFactory<ContentField>, ContentFieldFactory>();

        services.AddSingleton<IContentItemIdGenerator, DefaultContentItemIdGenerator>();
        services.AddScoped<IContentHandleManager, ContentHandleManager>();

        services.AddOptions<ContentOptions>();
        services.AddScoped<IContentFieldSetHandlerResolver, ContentFieldSetHandlerResolver>();
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
