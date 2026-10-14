using System.Security.Claims;
using Crest.Access;
using Crest.Admin;
using Crest.Navigation;
using Crest.Routing;
using Crest.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Crest.Services;

/// <summary>
/// The served admin menu, a view over the page registry (docs/admin-menu.md › The page registry),
/// assembled once here for everyone who needs it: the registry's admin entries form the default
/// tree, each item shown exactly when the caller may open its page (the route gate's own
/// question, <see cref="IRouteAuthorization.AllowsAnyAsync"/>); captions resolve through the
/// tenant translation store; the tenant's edits (the defined layer, by entry id) apply next, and
/// the user's own hidden items last. The sidebar endpoint, the app manifest and any page that
/// lists what the menu lists (All Parties) agree on this tree, so none of them assembles it.
/// </summary>
public sealed class AdminMenuBuilder(
    IPageRegistry registry,
    IRouteAuthorization routeAuthorization,
    ICallerContextAccessor callers,
    LinkGenerator linkGenerator,
    IOptions<AdminOptions> adminOptions,
    MenuCaptionResolver captionResolver,
    AdminMenuLayoutService layoutService,
    UserMenuPreferencesService userMenuPreferences)
{
    public const string MenuName = "admin";

    /// <summary>
    /// The translation context the menu's captions are stored under in the tenant translation
    /// store ("Admin Menus:Primary Navigation"), where the Translations editor lists them.
    /// </summary>
    public const string TranslationMenuName = "Primary Navigation";

    public async Task<NavigationMenu> BuildAsync(ActionContext actionContext, ClaimsPrincipal user, bool applyUserPreferences = true)
    {
        // Crest has already authorized the tree for the caller; the tenant-wide layout applies
        // only afterwards, and the user's own overlay after that.
        var menu = await layoutService.ApplyAsync(await BuildDefaultAsync(actionContext));
        return applyUserPreferences ? await userMenuPreferences.ApplyAsync(menu, user) : menu;
    }

    /// <summary>The registry's default tree for the caller, before any tenant or user layer: what the menu editor edits.</summary>
    public async Task<NavigationMenu> BuildDefaultAsync(ActionContext actionContext)
    {
        var pages = await registry.GetAsync(actionContext.HttpContext.RequestAborted);
        await captionResolver.EnsureLoadedAsync();

        var entries = pages.InShell(ShellId.Admin).Where(entry => entry.Menu is not null).ToArray();
        var ids = entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var children = entries.ToLookup(entry => entry.Menu!.Parent is { } parent && ids.Contains(parent) ? parent : null, StringComparer.Ordinal);

        var context = new BuildContext(actionContext, callers.Current, "/" + adminOptions.Value.AdminUrlPrefix.Trim('/'));
        return new NavigationMenu(MenuName, await BuildLevelAsync(children, null, context));
    }

    private async Task<NavigationItem[]> BuildLevelAsync(ILookup<string?, PageEntry> children, string? parent, BuildContext context)
    {
        var items = new List<(NavigationItem Item, PageEntry Entry)>();
        foreach (var entry in children[parent])
        {
            var childItems = await BuildLevelAsync(children, entry.Id, context);
            var allowed = entry.Permissions.Count > 0
                && await routeAuthorization.AllowsAnyAsync(context.Caller, entry.Permissions, context.ActionContext.HttpContext.RequestAborted);

            // An item opens its page for a caller the page admits; a section stays while any of
            // its children is shown, without a link of its own when its page does not admit them.
            if (!allowed && childItems.Length == 0)
            {
                continue;
            }

            items.Add((ToItem(entry, allowed, childItems, context), entry));
        }

        return [.. items
            .OrderBy(pair => pair.Item.Position, NavigationPositionComparer.Instance)
            .ThenBy(pair => pair.Entry.Menu!.Caption, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Item)];
    }

    private NavigationItem ToItem(PageEntry entry, bool allowed, NavigationItem[] childItems, BuildContext context)
    {
        var menu = entry.Menu!;
        string? url = null;
        string? href = null;
        if (allowed)
        {
            if (menu.LegacyRoute is { } legacy)
            {
                href = linkGenerator.GetPathByRouteValues(context.ActionContext.HttpContext, routeName: null, new RouteValueDictionary(legacy));
            }
            else if (!string.IsNullOrEmpty(menu.Url))
            {
                url = context.AdminPath + menu.Url;
            }
        }

        var text = captionResolver.Resolve(menu.Caption, menu.CaptionIsData ? null : TranslationMenuName, entry.DeclaredBy);
        return new NavigationItem(
            text,
            menu.Caption,
            entry.Id,
            href,
            url,
            null,
            // A sibling without a position sorts after the numbered ones, by caption (the way a
            // caption-derived position always has).
            menu.Position ?? menu.Caption,
            null,
            string.IsNullOrWhiteSpace(menu.Icon) ? [] : [NavigationConstants.CssClassPrefix + menu.Icon],
            childItems);
    }

    private sealed record BuildContext(ActionContext ActionContext, CallerContext? Caller, string AdminPath);
}
