using Crest.Access;
using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Contents;

namespace Crest.Parties.Permissions;

/// <summary>All Parties plus one concrete route per tenant-kind party type (a global kind's page is its owner's to gate).</summary>
public sealed class PartiesRoutePermissionProvider(PartyTypeCatalog catalog) : IRoutePermissionProvider
{
    public IEnumerable<RoutePermission> GetRoutes()
    {
        yield return new(PartiesConstants.Routes.AllPartiesAdmin, CommonPermissions.ListContent.Name);
        foreach (var type in catalog.Kinds.Where(type => type.Kind == PartyTypeKinds.Tenant))
        {
            yield return new(PartyTypeCatalog.RouteOf(type), CommonPermissions.ListContent.Name);
        }
    }
}
