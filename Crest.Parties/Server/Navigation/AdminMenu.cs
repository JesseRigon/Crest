using Crest.Parties.Constants;
using Crest.Parties.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace Crest.Parties.Navigation;

// Blazor-only pages with no MVC controller action to route to, so .Url(...) is the only
// option here. Building the URL from the real AdminOptions.AdminUrlPrefix keeps the links
// correct under a custom prefix - see Crest's own CrestAdminMenu for the same fix and
// rationale. AdminUrlPrefix replaces the literal word "Admin" in every URL (see
// AdminAreaControllerRouteMapper upstream), it isn't layered on top of it, so the real URL
// is "{realAdminPrefix}/Parties/Customers", matching the page's base-relative "@page"
// directive, resolved by Blazor's Router relative to <base href>.
//
// The root item is the All Parties page; one child per registered party type. The
// children are what the tenant's layout overlay and a user's own preference hide, and
// All Parties reads the same served items to decide which panes to show.
public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions, PartyTypeCatalog catalog) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Parties"], "60", parties =>
        {
            parties
                .AddClass("parties")
                .AddClass("icon-class-fa fa-address-book")
                .Id("parties")
                .Url($"{adminPath}{PartiesConstants.Routes.AllPartiesAdmin}")
                .Permission(CommonPermissions.ListContent);

            // The primary nav turns a parent with children into an expander, never a link, so
            // the root's own page needs a child to reach it.
            parties.Add(S["All Parties"], "0", item => item
                .AddClass("all-parties")
                .AddClass("icon-class-fa fa-address-book")
                .Id("parties-all")
                .Url($"{adminPath}{PartiesConstants.Routes.AllPartiesAdmin}")
                .Permission(CommonPermissions.ListContent)
                .LocalNav());

            var position = 0;
            foreach (var type in catalog.Types)
            {
                position++;
                var route = PartyTypeCatalog.RouteOf(type);
                parties.Add(S[type.DisplayName], position.ToString(), item =>
                {
                    item.AddClass(type.Key)
                        .Id(PartyTypeCatalog.MenuItemIdOf(type))
                        .Url($"{adminPath}{route}")
                        .Permission(CommonPermissions.ListContent)
                        .LocalNav();
                    if (!string.IsNullOrWhiteSpace(type.Icon))
                    {
                        item.AddClass("icon-class-" + type.Icon);
                    }
                });
            }
        });

        return ValueTask.CompletedTask;
    }
}
