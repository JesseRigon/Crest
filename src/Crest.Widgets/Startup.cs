using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.DisplayManagement;
using Crest.Modules;
using Crest.Widgets.Drivers;
using Crest.Widgets.Models;
using Crest.Widgets.Services;
using Crest.Widgets.Settings;

namespace Crest.Widgets;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Add Widget Card Shapes
        services.AddShapeTableProvider<ContentCardShapes>();
        // Widgets List Part
        services.AddContentPart<WidgetsListPart>()
            .UseDisplayDriver<WidgetsListPartDisplayDriver>();

        services.AddScoped<IStereotypesProvider, WidgetStereotypesProvider>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, WidgetsListPartSettingsDisplayDriver>();
        services.AddContentPart<WidgetMetadata>();
        services.AddDataMigration<Migrations>();
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
