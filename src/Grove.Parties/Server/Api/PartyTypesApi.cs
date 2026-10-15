using Grove.Api;
using Grove.Infrastructure.Shells.Admin;
using Grove.Parties.Constants;
using Grove.Parties.PartyTypes;
using Grove.Parties.Services;
using Grove.Services;
using Grove.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Parties.Api;

/// <summary>
/// <c>api/grove/parties/types</c>: the party types a user may work with, in menu order, each
/// carrying the key of its served Parties menu item. The All Parties page's left-hand list is
/// this list: a type the tenant's admin-menu layout hides is absent (same overlay as the
/// sidebar: the tenant hid it for everyone); a type the user hid for themselves is flagged, so
/// the page can keep it out of the list yet offer it back in its pane settings.
/// </summary>
public sealed class PartyTypesApi : ApiContributor
{
    public override string Name => "PartyTypes";

    public override string FeatureId => "Grove.Parties";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("parties");
        routes.MapGet(PartiesConstants.Routes.Types, ApiHandlers.Handle<PartyTypesHandlers>(h => h.ListAsync()));
    }
}

public sealed class PartyTypesHandlers(
    IAuthorizationService authorization,
    AdminMenuBuilder adminMenuBuilder,
    UserMenuPreferencesService userMenuPreferences,
    PartyTypeCatalog catalog,
    IOptions<AdminOptions> adminOptions) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel))
        {
            return Forbid();
        }

        // The tree the sidebar serves, before the user's own overlay, so a user-hidden type
        // is still found and flagged. Keys are the page registry entry ids (KindPageSource).
        var menu = await adminMenuBuilder.BuildAsync(HttpContext, User, applyUserPreferences: false);

        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        var root = Flatten(menu.Items).FirstOrDefault(item => UrlMatches(item, adminPath + PartiesConstants.Routes.AllPartiesAdmin));
        var hiddenByUser = (await userMenuPreferences.GetAsync(User)).HiddenItemKeys.ToHashSet(StringComparer.Ordinal);

        var models = new List<PartyTypeModel>();
        foreach (var type in catalog.Kinds)
        {
            var route = PartyTypeCatalog.RouteOf(type);
            var menuItem = root is null ? null : Flatten(root.Items).FirstOrDefault(item => UrlMatches(item, adminPath + route));
            if (menuItem is null)
            {
                // Not served: the tenant hid it, or Grove's permission filter dropped it.
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

        return Ok(models);
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

    // Grove may prefix a served href with the application path base; the tail is ours.
    private static bool UrlMatches(NavigationItem item, string url) =>
        item.Link?.TrimEnd('/').EndsWith(url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) == true;
}
