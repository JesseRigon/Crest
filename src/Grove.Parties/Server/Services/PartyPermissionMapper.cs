using Grove.Fields;
using Grove.Access;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Contents;
using Grove.Contents.Security;
using Grove.Parties.Constants;
using Grove.Parties.Indexes;
using YesSql;

namespace Grove.Parties.Services;

/// <summary>
/// The parties rules (docs/access.md § Members, ruling 2026-10-13), on top of the content
/// mapping a party item gets anyway:
/// <list type="bullet">
/// <item>a Person whose portal user is the caller is their own record: viewing and editing it
/// (its contact points and positions with it) is granted by <c>ManageOwnOrgMember</c>;</item>
/// <item>an organization's contacts - the Organization itself and every Person holding a
/// position there - are viewed and edited with <c>ManageOrgMembers</c>, re-asked against that
/// organization, so the Members mapper decides the reach (staff every organization, a member
/// only their own).</item>
/// </list>
/// The content candidates (the per-type and own-item variations) stay, so nothing a content
/// permission granted before is taken away.
/// </summary>
public sealed class PartyPermissionMapper(ISession session) : IResourcePermissionMapper
{
    private static readonly ContentResourcePermissionMapper s_content = new();

    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (resource is not ContentItem { ContentType: PartiesConstants.ContentTypes.Person or PartiesConstants.ContentTypes.Organization } party
            || (permission != CommonPermissions.ViewContent.Name && permission != CommonPermissions.EditContent.Name))
        {
            return null;
        }

        var candidates = new List<PermissionCandidate>(await s_content.MapAsync(permission, resource, caller, cancellationToken) ?? [permission]);

        if (party.ContentType == PartiesConstants.ContentTypes.Person)
        {
            var portalUsers = party.Get<ContentFieldSet>(PartiesConstants.ContentTypes.Person)?.Get<OptionListField>(PartyUserLinkService.PortalUserField)?.SelectedIds ?? [];
            if (caller.UserId is not null && portalUsers.Contains(caller.UserId, StringComparer.Ordinal))
            {
                candidates.Add(OrganizationPermissionNames.ManageOwnOrgMember);
            }

            var partyId = party.ContentItemId;
            var organizations = (await session.QueryIndex<PartyPositionIndex>(index => index.ContentItemId == partyId && index.Latest).ListAsync(cancellationToken))
                .Select(row => row.OrganizationId)
                .Distinct(StringComparer.Ordinal);
            foreach (var organizationId in organizations)
            {
                candidates.Add(new PermissionCandidate(OrganizationPermissionNames.ManageOrgMembers, new OrganizationResource(organizationId)));
            }
        }
        else
        {
            candidates.Add(new PermissionCandidate(OrganizationPermissionNames.ManageOrgMembers, new OrganizationResource(party.ContentItemId)));
        }

        return candidates;
    }
}
