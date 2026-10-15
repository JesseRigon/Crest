using Grove.Data.Regions.Fields;
using Grove.Data.Regions.Indexes;
using Grove.Data.Regions.Migrations;
using Grove.Data.Regions.Models;
using Grove.Data.Regions.Services;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Api;
using Grove.Modules;
using Grove.Display.Navigation;

namespace Grove.Data.Regions;

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
        services.AddApiContributor<Api.AddressingMapsApi>();
        services.AddApiContributor<Api.GeoImportApi>();
    }
}
