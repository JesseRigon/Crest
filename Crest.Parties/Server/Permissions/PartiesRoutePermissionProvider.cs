using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Services;
using OrchardCore.Contents;

namespace Crest.Parties.Permissions;

/// <summary>All Parties plus one concrete route per tenant-kind party type (a global kind's page is its owner's to gate).</summary>
public sealed class PartiesRoutePermissionProvider(PartyTypeCatalog catalog) : ICrestRoutePermissionProvider
{
    public IEnumerable<CrestRoutePermission> GetRoutes()
    {
        yield return new(PartiesConstants.Routes.AllPartiesAdmin, CommonPermissions.ListContent);
        foreach (var type in catalog.Types.Where(type => type.Kind == PartyTypeKinds.Tenant))
        {
            yield return new(PartyTypeCatalog.RouteOf(type), CommonPermissions.ListContent);
        }
    }
}
