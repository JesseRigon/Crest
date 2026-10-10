using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Indexing;
using Crest.Modules;
using Crest.Spatial.Drivers;
using Crest.Spatial.Fields;
using Crest.Spatial.Handlers;
using Crest.Spatial.Indexing;
using Crest.Spatial.ViewModels;

namespace Crest.Spatial;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        // Coordinate Field
        services.AddContentField<GeoPointField>()
            .UseDisplayDriver<GeoPointFieldDisplayDriver>()
            .AddHandler<GeoPointFieldHandler>();

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, GeoPointFieldSettingsDriver>();
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
