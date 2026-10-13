using Crest.Services;
using Crest.Icons;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.Navigation;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/navigation")]
public sealed class NavigationController(
    IRequestAccess requestAccess) : ControllerBase
{
    [HttpGet("admin")]
    public Task<ActionResult<NavigationMenu>> GetAdminMenu() => GetMenu("admin");

    // Resolves the tenant's single MenuPlacement.User AdminMenu document into a
    // NavigationMenu. See ProfileMenuService for why this can't go through
    // INavigationManager.BuildMenuAsync the way "admin" does.
    [HttpGet("profile")]
    public async Task<ActionResult<NavigationMenu>> GetProfileMenuAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var profileMenuService = access.GetRequiredService<ProfileMenuService>();
        return Ok(await profileMenuService.BuildAsync(User, HttpContext.RequestAborted));
    }

    // Self-service, like api/crest/localization/me: the current user's own hidden admin
    // menu items. Not a management endpoint - there is no user id in the route.
    [HttpGet("me/hidden")]
    public async Task<ActionResult<UserHiddenMenuItems>> GetMyHiddenItemsAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var preferences = await access.GetRequiredService<UserMenuPreferencesService>().GetAsync(User);
        return Ok(new UserHiddenMenuItems(preferences.HiddenItemKeys.ToArray()));
    }

    [HttpPut("me/hidden")]
    public async Task<ActionResult<UserHiddenMenuItems>> SetMyHiddenItemsAsync(UserHiddenMenuItems request)
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

    [HttpGet("menus/{menuName}")]
    public async Task<ActionResult<NavigationMenu>> GetMenu(string menuName)
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
            menu = await access.GetRequiredService<AdminMenuBuilder>().BuildAsync(ControllerContext, User);
            menu = menu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        }
        else
        {
            var items = await access.GetRequiredService<INavigationManager>().BuildMenuAsync(menuName, ControllerContext);
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
