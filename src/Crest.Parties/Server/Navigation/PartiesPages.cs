using Crest.Contents;
using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Routing;

namespace Crest.Parties.Navigation;

/// <summary>
/// The Parties section (the generic kind-registry pages, <see cref="KindPageSource{TKind}"/>):
/// All Parties, then one page per registered party type. A tenant-kind type is served by the
/// generic party type page and gated at its concrete route; a global kind's page is its owner's.
/// The children are what the tenant's menu edits and a user's own preference hide, and All
/// Parties reads the same served items to decide which panes to show.
/// </summary>
public sealed class PartiesPages(PartyTypeCatalog catalog) : KindPageSource<PartyTypeDescriptor>(catalog)
{
    protected override Section Describe() => new(
        "Parties", "60", "parties", "fa fa-address-book",
        PartiesConstants.Routes.AllPartiesAdmin, "All Parties", CommonPermissions.ListContent.Name);

    protected override string GenericRoute(string key) => PartiesConstants.Routes.PartyTypeAdmin(key);

    protected override string EntryIdOf(Section section, PartyTypeDescriptor kind) => PartyTypeCatalog.MenuItemIdOf(kind);

    protected override bool Serves(PartyTypeDescriptor kind) => kind.Kind == PartyTypeKinds.Tenant;
}
