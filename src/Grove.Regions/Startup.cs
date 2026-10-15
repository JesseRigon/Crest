using Grove.Regions.Fields;
using Grove.Regions.Indexes;
using Grove.Regions.Migrations;
using Grove.Regions.Models;
using Grove.Regions.Services;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Navigation;

namespace Grove.Regions;

[Feature(RegionsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<GroveRegionalProfilePart>();
        services.AddContentPart<GroveRegionalProfileReferencePart>();
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

        // The addressing-map editor (Default tenant): menu entry and page gate.
    }
}
