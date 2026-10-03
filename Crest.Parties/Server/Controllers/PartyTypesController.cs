using Crest.Services;
using Crest.ViewModels;
using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace Crest.Parties.Controllers;

/// <summary>
/// The party types a user may work with, in menu order, each carrying the key of its
/// served Parties menu item. The All Parties page's left-hand list is this list: a type
/// the tenant's admin-menu layout hides is absent (same overlay as the sidebar - the
/// tenant hid it for everyone); a type the user hid for themselves is flagged, so the
/// page can keep it out of the list yet offer it back in its pane settings.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route(PartiesConstants.Routes.Api)]
public sealed class PartyTypesController(
    IAuthorizationService authorization,
    CrestAdminMenuBuilder adminMenuBuilder,
    CrestUserMenuPreferencesService userMenuPreferences,
    PartyTypeCatalog catalog,
    IOptions<AdminOptions> adminOptions) : ControllerBase
{
    [HttpGet(PartiesConstants.Routes.Types)]
    public async Task<ActionResult<IReadOnlyList<PartyTypeModel>>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel))
        {
            return Forbid();
        }

        // The tree the sidebar serves, before the user's own overlay, so a user-hidden type
        // is still found and flagged. Keys are the served keys - synced node UniqueIds, never
        // the provider's slug (see CrestProviderMenuSyncService for why the slug does not survive).
        var menu = await adminMenuBuilder.BuildAsync(ControllerContext, User, applyUserPreferences: false);

        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        var root = Flatten(menu.Items).FirstOrDefault(item => UrlMatches(item, adminPath + PartiesConstants.Routes.AllPartiesAdmin));
        var hiddenByUser = (await userMenuPreferences.GetAsync(User)).HiddenItemKeys.ToHashSet(StringComparer.Ordinal);

        var models = new List<PartyTypeModel>();
        foreach (var type in catalog.Types)
        {
            var route = PartyTypeCatalog.RouteOf(type);
            var menuItem = root is null ? null : Flatten(root.Items).FirstOrDefault(item => UrlMatches(item, adminPath + route));
            if (menuItem is null)
            {
                // Not served: the tenant hid it, or Orchard's permission filter dropped it.
                continue;
            }

            models.Add(new PartyTypeModel(
                type.Key,
                type.DisplayName,
                type.Kind,
                type.ContentType,
                route,
                type.Icon,
                type.Position,
                menuItem.Key,
                menuItem.Key is not null && hiddenByUser.Contains(menuItem.Key)));
        }

        return models;
    }

    private static IEnumerable<NavigationItem> Flatten(IEnumerable<NavigationItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Items))
            {
                yield return child;
            }
        }
    }

    // Orchard may prefix a served href with the application path base; the tail is ours.
    private static bool UrlMatches(NavigationItem item, string url) =>
        item.Link?.TrimEnd('/').EndsWith(url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) == true;
}
