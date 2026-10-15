using Grove.Access;
using Grove.Access.Members.Constants;
using Grove.Access.Members.Indexes;
using Grove.Access.Members.Permissions;
using YesSql;

namespace Grove.Access.Members.Services;

/// <summary>An organization's members as the resource <c>ManageOrgMembers</c> is asked against (list, create).</summary>
public sealed record MemberOrganizationResource(string OrganizationId)
{
    public override string ToString() => $"organization {OrganizationId}";
}

/// <summary>One user's binding to an organization as the resource <c>ManageOrgMembers</c> is asked against (add, remove).</summary>
public sealed record MemberBindingResource(string UserId, string OrganizationId)
{
    public override string ToString() => $"binding of {UserId} to {OrganizationId}";
}

/// <summary>
/// Members' resource mapper (docs/access.md § Members, ruling 2026-10-13): decides
/// <c>ManageOrgMembers</c> against an organization (<see cref="OrganizationResource"/>,
/// <see cref="MemberOrganizationResource"/>) or a binding. The same permission at a different
/// reach, read from the caller's acting organization, never from the side: a caller acting in
/// no organization (staff, at tenant reach) answers with the permission for every
/// organization; a caller acting in one answers only for that one, and for a binding only when
/// the user already holds a member binding there (the root guard). <c>ManageMembers</c> stays
/// staff-only and is not mapped.
/// </summary>
public sealed class MemberResourcePermissionMapper(ISession session) : IResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (permission != OrganizationPermissionNames.ManageOrgMembers)
        {
            return null;
        }

        var (organizationId, userId) = resource switch
        {
            OrganizationResource organization => (organization.OrganizationId, (string?)null),
            MemberOrganizationResource organization => (organization.OrganizationId, null),
            MemberBindingResource binding => (binding.OrganizationId, binding.UserId),
            OrganizationMemberResource member => (member.OrganizationId, member.UserId),
            _ => (null, null),
        };

        if (organizationId is null)
        {
            return null;
        }

        if (caller.OrganizationId is not null && !string.Equals(caller.OrganizationId, organizationId, StringComparison.Ordinal))
        {
            return [];
        }

        // The root guard holds at every reach for a member of an organization's groups: staff
        // are never members of them, so only the organization's members may be put there.
        if (caller.OrganizationId is null && resource is not OrganizationMemberResource)
        {
            return [permission];
        }

        if (userId is not null)
        {
            var inRoot = await session.QueryIndex<UserConnectionIndex>(index => index.UserId == userId && index.OrganizationId == organizationId && index.Class == UserClasses.Member)
                .CountAsync(cancellationToken) > 0;
            if (!inRoot)
            {
                return [];
            }
        }

        return [permission];
    }

    /// <summary>A member-class caller or an organization system actor: bound to one organization.</summary>
    public static bool ActsInOrganization(CallerContext caller) =>
        caller.UserClass == UserClasses.Member || (caller.IsSystem && caller.OrganizationId is not null);
}

/// <summary>
/// The rows of <see cref="UserConnectionIndex"/> a caller may read (docs/access.md, Domains
/// onto the extension system): the super user, staff and the tenant system actor read every
/// organization's bindings (the permission bounds what they do with them); a caller acting in
/// an organization reads that organization's bindings only, and none off the member side or
/// with no organization; an anonymous caller reads none.
/// </summary>
public sealed class UserConnectionScopeProvider : IScopeProvider
{
    public string Target => nameof(UserConnectionIndex);

    public int Version => 2;

    public Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (caller.IsSuperUser || (caller.IsSystem && caller.OrganizationId is null))
        {
            return Task.FromResult(ScopeRule.All);
        }

        if (MemberResourcePermissionMapper.ActsInOrganization(caller))
        {
            return Task.FromResult(caller.OrganizationId is null || caller.Side is not (CallerSide.Member or CallerSide.System)
                ? ScopeRule.None
                : ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal(nameof(UserConnectionIndex.OrganizationId), caller.OrganizationId))));
        }

        return Task.FromResult(caller.IsAuthenticated ? ScopeRule.All : ScopeRule.None);
    }
}
