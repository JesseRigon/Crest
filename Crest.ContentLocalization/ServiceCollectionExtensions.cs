using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.ContentLocalization.Drivers;
using Crest.ContentLocalization.Handlers;
using Crest.ContentLocalization.Indexes;
using Crest.ContentLocalization.Models;
using Crest.ContentLocalization.Records;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Data;
using Crest.Data.Migration;

namespace Crest.ContentLocalization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentLocalization(this IServiceCollection services)
    {
        services.AddContentPart<LocalizationPart>()
            .UseDisplayDriver<LocalizationPartDisplayDriver>()
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
