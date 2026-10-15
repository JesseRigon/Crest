using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Grove.Entities;
using Grove.Users;
using Grove.Users.Models;
using Grove.ViewModels;

namespace Grove.Services;

/// <summary>
/// The user's own admin-menu preferences, on User.Properties the way Grove.Users.
/// Localization keeps UserLocalizationSettings there (docs/user-settings.md sketches
/// exactly this shape). Nothing in it is tenant-wide.
/// </summary>
public sealed class GroveUserMenuPreferences
{
    /// <summary>
    /// Served menu item keys (NavigationItem.Key - the synced node's UniqueId) the user has
    /// hidden for themselves. The tenant's layout overlay hides for everyone; this hides
    /// for one person, and a page that lists what the menu lists (All Parties' panes)
    /// honours it by the same key.
    /// </summary>
    public List<string> HiddenItemKeys { get; set; } = [];
}

/// <summary>
/// The per-user layer under the tenant-wide <see cref="AdminMenuLayoutService"/>:
/// applied after it, on the same served tree, so a key hidden by the tenant never reaches
/// here and a key hidden by the user vanishes with its children. Self-service only - a
/// user edits nobody's preference but their own.
/// </summary>
public sealed class UserMenuPreferencesService(UserManager<IUser> userManager)
{
    public async Task<GroveUserMenuPreferences> GetAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new GroveUserMenuPreferences();
        }

        var user = await userManager.GetUserAsync(principal) as User;
        if (user is null || !user.TryGet<GroveUserMenuPreferences>(out var preferences))
        {
            return new GroveUserMenuPreferences();
        }

        return preferences;
    }

    public async Task<GroveUserMenuPreferences?> SetHiddenItemKeysAsync(ClaimsPrincipal principal, IEnumerable<string> hiddenItemKeys)
    {
        var user = await userManager.GetUserAsync(principal) as User;
        if (user is null)
        {
            return null;
        }

        var keys = hiddenItemKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        user.Alter<GroveUserMenuPreferences>(preferences => preferences.HiddenItemKeys = keys);
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? new GroveUserMenuPreferences { HiddenItemKeys = keys } : null;
    }

    public async Task<NavigationMenu> ApplyAsync(NavigationMenu menu, ClaimsPrincipal principal)
    {
        var preferences = await GetAsync(principal);
        if (preferences.HiddenItemKeys.Count == 0)
        {
            return menu;
        }

        var hidden = preferences.HiddenItemKeys.ToHashSet(StringComparer.Ordinal);
        return menu with { Items = Apply(menu.Items, hidden) };
    }

    private static NavigationItem[] Apply(NavigationItem[] items, HashSet<string> hidden) => items
        .Where(item => item.Key is null || !hidden.Contains(item.Key))
        .Select(item => item with { Items = Apply(item.Items, hidden) })
        .ToArray();
}
