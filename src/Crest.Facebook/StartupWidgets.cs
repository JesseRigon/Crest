using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.DisplayManagement;
using Crest.Facebook.Widgets;
using Crest.Facebook.Widgets.Drivers;
using Crest.Facebook.Widgets.Handlers;
using Crest.Facebook.Widgets.Models;
using Crest.Facebook.Widgets.Services;
using Crest.Facebook.Widgets.Settings;
using Crest.Modules;

namespace Crest.Facebook;

[Feature(FacebookConstants.Features.Widgets)]
public sealed class StartupWidgets : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<WidgetMigrations>();
        services.AddShapeTableProvider<LiquidShapes>();

        services.AddContentPart<FacebookPluginPart>()
            .UseDisplayDriver<FacebookPluginPartDisplayDriver>()
            .AddHandler<FacebookPluginPartHandler>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, FacebookPluginPartSettingsDisplayDriver>();
    }
}
