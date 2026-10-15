using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.ContentLocalization.Handlers;
using Grove.ContentLocalization.Indexes;
using Grove.ContentLocalization.Models;
using Grove.ContentLocalization.Records;
using Grove.ContentManagement;
using Grove.ContentManagement.Handlers;
using Grove.Data;
using Grove.Data.Migration;

namespace Grove.ContentLocalization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentLocalization(this IServiceCollection services)
    {
        services.AddContentPart<LocalizationPart>()
            .AddHandler<LocalizationPartHandler>();

        services.TryAddScoped<IContentLocalizationManager, DefaultContentLocalizationManager>();

        services.AddScoped<LocalizedContentItemIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<LocalizedContentItemIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<LocalizedContentItemIndexProvider>());

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentLocalizationHandler, ContentLocalizationPartHandlerCoordinator>();

        return services;
    }
}
