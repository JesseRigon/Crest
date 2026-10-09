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
        services.AddContentPart<CrestRegionalProfilePart>();
        services.AddContentPart<CrestRegionalProfileReferencePart>();
        services.AddContentField<GeoStackField>();
        services.AddDataMigration<RegionsMigrations>();
        services.AddIndexProvider<RegionalProfileIndexProvider>();
        services.AddIndexProvider<GeoTenantNodeIndexProvider>();
        services.AddIndexProvider<GeoNodeOverrideIndexProvider>();
        services.AddScoped<IRegionalProfileResolver, RegionalProfileResolver>();
        services.AddScoped<IGeoService, GeoService>();
        // Provider seams (docs/regions.md): the built-in point-in-polygon
        // ships; geocoders are external and registered by the host.
        services.AddScoped<IGeoBoundaryResolver, NetTopologySuiteBoundaryResolver>();
        services.AddScoped<IGeoLocator, CompositeGeoLocator>();
    }
}
