using Crest.Regions.Fields;
using Crest.Regions.Indexes;
using Crest.Regions.Migrations;
using Crest.Regions.Models;
using Crest.Regions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace Crest.Regions;

[Feature(RegionsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<CrestLocalizationProfilePart>();
        services.AddContentPart<CrestLocalizationProfileReferencePart>();
        services.AddContentField<GeoStackField>();
        services.AddDataMigration<RegionsMigrations>();
        services.AddIndexProvider<LocalizationProfileIndexProvider>();
        services.AddIndexProvider<GeoTenantNodeIndexProvider>();
        services.AddIndexProvider<GeoNodeOverrideIndexProvider>();
        services.AddScoped<ILocalizationProfileResolver, LocalizationProfileResolver>();
        services.AddScoped<IGeoService, GeoService>();
        // Provider seams (plans/regions-and-locations.md): the built-in point-in-polygon
        // ships; geocoders are external and registered by the host.
        services.AddScoped<IGeoBoundaryResolver, NetTopologySuiteBoundaryResolver>();
        services.AddScoped<IGeoLocator, CompositeGeoLocator>();
    }
}
