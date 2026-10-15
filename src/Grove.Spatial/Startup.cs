using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentTypes.Editors;
using Grove.Indexing;
using Grove.Modules;
using Grove.Spatial.Drivers;
using Grove.Spatial.Fields;
using Grove.Spatial.Handlers;
using Grove.Spatial.Indexing;
using Grove.Spatial.ViewModels;

namespace Grove.Spatial;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        // Coordinate Field
        services.AddContentField<GeoPointField>()
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
