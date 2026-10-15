using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data.Migration;
using Grove.Display.Management;
using Grove.Data.Localization;
using Grove.Contents.SiteMenu.Handlers;
using Grove.Contents.SiteMenu.Models;
using Grove.Contents.SiteMenu.Services;
using Grove.Contents.SiteMenu.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;

namespace Grove.Contents.SiteMenu;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddShapeTableProvider<MenuShapes>();
        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.StandardMenusApi>();

        services.AddScoped<IStereotypesProvider, MenuItemStereotypesProvider>();

        // MenuPart
        services.AddScoped<IContentHandler, MenuContentHandler>();
        services.AddContentPart<MenuPart>();

        services.AddContentPart<MenuItemsListPart>();

        // LinkMenuItemPart
        services.AddContentPart<LinkMenuItemPart>()
            .AddHandler<LinkMenuItemPartHandler>();

        // ContentMenuItemPart
        services.AddContentPart<ContentMenuItemPart>();

        // HtmlMenuItemPart
        services.AddContentPart<HtmlMenuItemPart>()
            .AddHandler<HtmlMenuItemPartHandler>();

        services.AddContentPart<MenuItemPermissionPart>();

    }
}
