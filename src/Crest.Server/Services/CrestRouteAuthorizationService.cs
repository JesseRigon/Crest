using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Crest.Admin;
using Crest.Contents;
using Crest.Indexing;
using Crest.Indexing.Core;
using Crest.Localization;
using Crest.Media;
using Crest.Recipes;
using Crest.Roles;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Settings;
using Crest.Users;

namespace Crest.Services;

/// <summary>
/// A batch, client-navigation projection of Crest permissions. This is only
/// a UI gate: the underlying Crest services and Crest adapters remain the
/// authority for every data operation.
/// </summary>
public sealed class CrestRouteAuthorizationService(
    IAuthorizationService authorization,
    IEnumerable<ICrestRoutePermissionProvider> providers)
{
    public async Task<CrestRouteAccess[]> GetAuthorizedRoutesAsync(ClaimsPrincipal user)
    {
        var granted = new List<CrestRouteAccess>();
        foreach (var route in GetRoutes())
        {
            if (await authorization.AuthorizeAsync(user, route.Permission))
            {
                granted.Add(new CrestRouteAccess(route.Template));
            }
        }

        return granted.ToArray();
    }

    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, string? path)
    {
        var route = GetRoutes().FirstOrDefault(candidate => Matches(candidate.Template, path));
        return route is not null && await authorization.AuthorizeAsync(user, route.Permission);
    }

    public static bool Matches(string template, string? path)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private CrestRoutePermission[] GetRoutes() => providers
        .SelectMany(provider => provider.GetRoutes())
        .GroupBy(route => route.Template, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();
}

public interface ICrestRoutePermissionProvider
{
    IEnumerable<CrestRoutePermission> GetRoutes();
}

/// <summary>
/// Admin routes (templates as in <see cref="ICrestRoutePermissionProvider"/>, relative to
/// the admin path) whose pages must run in the browser: the admin document for them is
/// rendered InteractiveWebAssembly instead of InteractiveAuto, as the login shell is. For
/// a page that hosts a client-only component library - one whose services and HTTP
/// clients exist only in the WASM client (Crest.Components.Modules.ICrestClientModule),
/// not in a server circuit.
/// </summary>
public interface ICrestWebAssemblyRouteProvider
{
    IEnumerable<string> GetWebAssemblyRoutes();
}

public sealed class CrestRoutePermissionProvider : ICrestRoutePermissionProvider
{
    public IEnumerable<CrestRoutePermission> GetRoutes() =>
    [
        // "" is the bare AdminPath request itself (e.g. "/backoffice" with no further
        // segment - adminRemainder is an empty string, not "/"). That's Home.razor's
        // own "@page "/"" route, which immediately redirects an authenticated user to
        // Dashboard - so it needs the same AccessAdminPanel gate as Dashboard itself,
        // just under Matches' own empty-template-matches-empty-path shape.
        new("", AdminPermissions.AccessAdminPanel),
        new("/Dashboard", AdminPermissions.AccessAdminPanel),
        new("/Features", Crest.Features.FeaturesPermissions.ManageFeatures),
        new("/Themes", Crest.Themes.Permissions.ApplyTheme),
        new("/AdminMenus", Crest.AdminMenu.AdminMenuPermissions.ManageAdminMenu),
        new("/AdminMenu", Crest.AdminMenu.AdminMenuPermissions.ManageAdminMenu),
        new("/AdminMenu/List", Crest.AdminMenu.AdminMenuPermissions.ManageAdminMenu),
        new("/Menus", Crest.Menu.Permissions.ManageMenu),
        new("/Menu", Crest.Menu.Permissions.ManageMenu),
        new("/Menu/List", Crest.Menu.Permissions.ManageMenu),
        new("/Contents/ContentItems/Menu", Crest.Menu.Permissions.ManageMenu),
        new("/Contents/ContentItems", Crest.Contents.CommonPermissions.ListContent),
        new("/Contents/ContentTypes/{ContentType}/Create", Crest.Contents.CommonPermissions.EditContent),
        new("/Contents/ContentItems/{ContentItemId}/Edit", Crest.Contents.CommonPermissions.EditContent),
        new("/ContentTypes/List", ContentTypesPermissions.ViewContentTypes),
        new("/ContentTypes/ContentPartLists", Crest.Permissions.CrestContentPartListPermissions.ViewContentPartLists),
        new("/ContentTypes/ListParts", ContentTypesPermissions.ViewContentTypes),
        new("/Users/Index", UsersPermissions.ListUsers),
        new("/Users/Create", UsersPermissions.EditUsers),
        new("/Users/Edit/{Id}", UsersPermissions.EditUsers),
        new("/Tenants", Crest.Tenants.Permissions.ManageTenants),
        new("/Roles/Index", RolesPermissions.ManageRoles),
        new("/Media", MediaPermissions.ManageMedia),
        new("/Media/Options", MediaPermissions.ViewMediaOptions),
        new("/MediaProfiles", MediaPermissions.ManageMediaProfiles),
        new("/indexing", IndexingPermissions.ManageIndexes),
        new("/Queries/Index", Crest.Queries.Permissions.ManageQueries),
        new("/Recipes", RecipePermissions.ManageRecipes),
        new("/Templates", Crest.Templates.Permissions.ManageTemplates),
        new("/Settings/general", SettingsPermissions.ManageSettings),
        new("/Settings/admin", AdminPermissions.ManageAdminSettings),
        new("/Settings/localization", LocalizationPermissions.ManageCultures),
        // The Crest translations editor (Pages/Translations.razor) shadows the stock
        // DataLocalization URLs so the existing admin menu link and the Localization page's
        // "Edit translations" button land on it instead of the legacy frame. View-level gate
        // only - the API enforces per-culture edit rights on top.
        new("/DataLocalization", Crest.Localization.Data.DataLocalizationPermissions.ViewDynamicTranslations),
        new("/DataLocalization/Index", Crest.Localization.Data.DataLocalizationPermissions.ViewDynamicTranslations),
        new("/Settings/userLogin", UsersPermissions.ManageUsers),
        new("/Settings/SecurityHeaders", SecurityPermissions.ManageSecurityHeadersSettings),
        new("/Settings", SecurityPermissions.ManageSecurityHeadersSettings),
        new("/Design/Icons", SettingsPermissions.ManageSettings),
        new("/Icons", SettingsPermissions.ManageSettings),
        new("/DesignSystem", SettingsPermissions.ManageSettings),
    ];
}

public sealed record CrestRoutePermission(string Template, Permission Permission);

public sealed record CrestRouteAccess(string Template);
