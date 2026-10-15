using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.DisplayManagement;
using Grove.Localization;
using Grove.Menu.Drivers;
using Grove.Menu.Handlers;
using Grove.Menu.Models;
using Grove.Menu.Services;
using Grove.Menu.Settings;
using Grove.Menu.TagHelpers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.Menu;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddShapeTableProvider<MenuShapes>();
        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.StandardMenusApi>();
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

        services.AddTagHelpers<MenuTagHelper>();
    }
}
