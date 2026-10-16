using Grove.Contents.Fields;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Indexing;
using Grove.Modules;
using Grove.Data.Types.Spatial.Drivers;
using Grove.Data.Types.Spatial.Fields;
using Grove.Data.Types.Spatial.Handlers;
using Grove.Data.Types.Spatial.Indexing;
using Grove.Data.Types.Spatial.ViewModels;

namespace Grove.Data.Types.Spatial;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {

        // Coordinate Field
        services.AddFieldType<GeoPointField, Grove.Data.Types.Spatial.Settings.GeoPointFieldSettings>()
            .UseDisplayDriver<GeoPointFieldDisplayDriver>()
            .AddHandler<GeoPointFieldHandler>();

        services.AddScoped<IContentFieldIndexHandler, GeoPointFieldIndexHandler>();

        // Registering both field types and shape types are necessary as they can
        // be accessed from inner properties.
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<GeoPointField>();
            o.MemberAccessStrategy.Register<DisplayGeoPointFieldViewModel>();
        });
    }
}
