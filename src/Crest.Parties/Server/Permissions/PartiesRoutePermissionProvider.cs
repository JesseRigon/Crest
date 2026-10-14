using Crest.Access;
using Crest.Contents;
using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;

namespace Crest.Parties.Permissions;

/// <summary>All Parties plus one concrete route per tenant-kind party type (a global kind's page is its owner's to gate).</summary>
public sealed class PartiesRoutePermissionProvider(PartyTypeCatalog catalog) : KindRoutePermissionProvider<PartyTypeDescriptor>(catalog)
{
    protected override string AllRoute => PartiesConstants.Routes.AllPartiesAdmin;

    protected override string Permission => CommonPermissions.ListContent.Name;

    protected override string GenericRoute(string key) => PartiesConstants.Routes.PartyTypeAdmin(key);

    protected override bool Serves(PartyTypeDescriptor kind) => kind.Kind == PartyTypeKinds.Tenant;
}
