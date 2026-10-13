using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Admin;
using Crest.Contents;
using Crest.Navigation;

namespace Crest.Parties.Navigation;

// The root item is the All Parties page; one child per registered party type (the generic
// kind-registry menu, Crest.Navigation's KindAdminMenu). The children are what the tenant's
// layout overlay and a user's own preference hide, and All Parties reads the same served
// items to decide which panes to show.
public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions, PartyTypeCatalog catalog)
    : KindAdminMenu<PartyTypeDescriptor>(adminOptions, catalog)
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override Section Describe() => new(
        S["Parties"], "60", "parties", "fa fa-address-book",
        PartiesConstants.Routes.AllPartiesAdmin, S["All Parties"], CommonPermissions.ListContent);

    protected override LocalizedString CaptionOf(PartyTypeDescriptor kind) => S[kind.DisplayName];

    protected override string RouteOf(PartyTypeDescriptor kind) => PartyTypeCatalog.RouteOf(kind);

    protected override string MenuItemIdOf(Section section, PartyTypeDescriptor kind) => PartyTypeCatalog.MenuItemIdOf(kind);
}
