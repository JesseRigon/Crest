using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.DisplayManagement;
using Crest.Localization;
using Crest.Menu.Drivers;
using Crest.Menu.Handlers;
using Crest.Menu.Models;
using Crest.Menu.Services;
using Crest.Menu.Settings;
using Crest.Menu.TagHelpers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.Menu;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddShapeTableProvider<MenuShapes>();
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IJSLocalizer, MenuJSLocalizer>();

        services.AddScoped<IStereotypesProvider, MenuItemStereotypesProvider>();

        // MenuPart
        services.AddScoped<IContentHandler, MenuContentHandler>();
        services.AddContentPart<MenuPart>()
            .UseDisplayDriver<MenuPartDisplayDriver>();

        services.AddContentPart<MenuItemsListPart>();

        // LinkMenuItemPart
        services.AddContentPart<LinkMenuItemPart>()
            .UseDisplayDriver<LinkMenuItemPartDisplayDriver>();

        // ContentMenuItemPart
        services.AddContentPart<ContentMenuItemPart>()
            .UseDisplayDriver<ContentMenuItemPartDisplayDriver>();

        // HtmlMenuItemPart
        services.AddContentPart<HtmlMenuItemPart>()
            .UseDisplayDriver<HtmlMenuItemPartDisplayDriver>();

        services.AddContentPart<MenuItemPermissionPart>()
            .UseDisplayDriver<MenuItemPermissionPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, HtmlMenuItemPartSettingsDisplayDriver>();
        services.AddTagHelpers<MenuTagHelper>();
    }
}
