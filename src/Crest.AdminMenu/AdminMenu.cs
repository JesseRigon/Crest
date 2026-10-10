using Microsoft.Extensions.Localization;
using Crest.AdminMenu.Services;
using Crest.Navigation;

namespace Crest.AdminMenu;

public sealed class AdminMenu : AdminNavigationProvider
{
    private readonly AdminMenuNavigationProvidersCoordinator _adminMenuNavigationProviderCoordinator;

    internal readonly IStringLocalizer S;

    public AdminMenu(
        AdminMenuNavigationProvidersCoordinator adminMenuNavigationProviderCoordinator,
        IStringLocalizer<AdminMenu> stringLocalizer)
    {
        _adminMenuNavigationProviderCoordinator = adminMenuNavigationProviderCoordinator;
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            // Configuration and settings menus for the AdminMenu module.
            builder
                .Add(S["Configuration"], configuration => configuration
                    .Add(S["Admin menus"], S["Admin menus"].PrefixPosition(), adminMenu => adminMenu
                        .Permission(AdminMenuPermissions.ManageAdminMenu)
                        .Action("List", "Menu", "Crest.AdminMenu")
                        .LocalNav()
                    )
                );
        }
        else
        {
            // Configuration and settings menus for the AdminMenu module.
            builder
                .Add(S["Tools"], tools => tools
                    .Add(S["Admin Menus"], S["Admin Menus"].PrefixPosition(), adminMenu => adminMenu
                        .Permission(AdminMenuPermissions.ManageAdminMenu)
                        .Action("List", "Menu", "Crest.AdminMenu")
                        .LocalNav()
                    )
                );
        }

        // This is the entry point for the adminMenu: dynamically generated custom admin menus.
        return _adminMenuNavigationProviderCoordinator.BuildNavigationAsync(NavigationConstants.AdminMenuId, builder);
    }
}
