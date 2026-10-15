using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Services;
using Grove.Display.Icons;
using Grove.Infrastructure.Shells.Admin;
using Grove.Display.Navigation;
using Grove.ViewModels;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/navigation</c> endpoints.</summary>
public sealed class NavigationApi : ApiContributor
{
    public override string Name => "Navigation";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("navigation");
        routes.MapGet("admin", ApiHandlers.Handle<NavigationHandlers>(h => h.GetAdminMenu()));
        routes.MapGet("profile", ApiHandlers.Handle<NavigationHandlers>(h => h.GetProfileMenuAsync()));
        routes.MapGet("me/hidden", ApiHandlers.Handle<NavigationHandlers>(h => h.GetMyHiddenItemsAsync()));
        routes.MapPut("me/hidden", (HttpContext http, [FromBody] UserHiddenMenuItems request) => ApiHandlers.For<NavigationHandlers>(http).SetMyHiddenItemsAsync(request));
        routes.MapGet("menus/{menuName}", (HttpContext http, [FromRoute] string menuName) => ApiHandlers.For<NavigationHandlers>(http).GetMenu(menuName));
    }
}

public sealed class NavigationHandlers(
    IRequestAccess requestAccess) : ApiHandlers
{
    public Task<IResult> GetAdminMenu() => GetMenu("admin");

    // Resolves the tenant's single MenuPlacement.User AdminMenu document into a
    // NavigationMenu. See ProfileMenuService for why this can't go through
    // INavigationManager.BuildMenuAsync the way "admin" does.
    public async Task<IResult> GetProfileMenuAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var profileMenuService = access.GetRequiredService<ProfileMenuService>();
        return Ok(await profileMenuService.BuildAsync(User, HttpContext.RequestAborted));
    }

    // Self-service, like api/grove/localization/me: the current user's own hidden admin
    // menu items. Not a management endpoint - there is no user id in the route.
    public async Task<IResult> GetMyHiddenItemsAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var preferences = await access.GetRequiredService<UserMenuPreferencesService>().GetAsync(User);
        return Ok(new UserHiddenMenuItems(preferences.HiddenItemKeys.ToArray()));
    }

    public async Task<IResult> SetMyHiddenItemsAsync(UserHiddenMenuItems request)
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var saved = await access.GetRequiredService<UserMenuPreferencesService>().SetHiddenItemKeysAsync(User, request.HiddenItemKeys ?? []);
        return saved is null
            ? StatusCode(StatusCodes.Status500InternalServerError)
            : Ok(new UserHiddenMenuItems(saved.HiddenItemKeys.ToArray()));
    }

    public async Task<IResult> GetMenu(string menuName)
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var primaryNavMenuSettingsStore = access.GetRequiredService<PrimaryNavMenuSettingsStore>();
        var iconController = access.GetRequiredService<IconController>();

        NavigationMenu menu;
        if (string.Equals(menuName, AdminMenuBuilder.MenuName, StringComparison.OrdinalIgnoreCase))
        {
            await access.GetRequiredService<AdminSettingsNormalizer>().EnsureNewMenuEnabledAsync();
            menu = await access.GetRequiredService<AdminMenuBuilder>().BuildAsync(HttpContext, User);
            menu = menu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        }
        else
        {
            var items = await access.GetRequiredService<INavigationManager>().BuildMenuAsync(menuName, HttpContext);
            var captionResolver = access.GetRequiredService<MenuCaptionResolver>();
            await captionResolver.EnsureLoadedAsync();
            menu = new NavigationMenu(
                menuName,
                items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                    .Select(item => NavigationItem.From(item, captionResolver))
                    .ToArray());
        }

        return Ok(await iconController.ResolveMenuIconsAsync(
            menu,
            string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase) ? IconController.AdminMenuChromeIconKeys : null,
            HttpContext.RequestAborted));
    }
}
