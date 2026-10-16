using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Contents.Localization.Handlers;
using Grove.Contents.Localization.Indexes;
using Grove.Contents.Localization.Models;
using Grove.Contents.Localization.Records;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;

namespace Grove.Contents.Localization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentLocalization(this IServiceCollection services)
    {
        // The Localization field set: its data is its fields; its behaviour, the module's system
        // flows; its culture, a read projection.
        services.AddContentFieldSet<LocalizationFieldSet>();
        services.AddScoped<Services.LocalizationBehaviour>();
        services.AddContentFieldSetProjection<Projections.LocalizationCultureProjection>();

        services.TryAddScoped<IContentLocalizationManager, DefaultContentLocalizationManager>();

        services.AddScoped<LocalizedContentItemIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<LocalizedContentItemIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<LocalizedContentItemIndexProvider>());

        services.AddDataMigration<Migrations>();
        services.AddScoped<IContentLocalizationHandler, ContentLocalizationFieldSetHandlerCoordinator>();

        return services;
    }
}
