using Crest.Access;
using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Permissions;
using YesSql;

namespace Crest.Members.Services;

/// <summary>An organization's members as the resource <c>ManageMembers</c> is asked against (list, create).</summary>
public sealed record MemberOrganizationResource(string OrganizationId)
{
    public override string ToString() => $"organization {OrganizationId}";
}

/// <summary>One user's binding to an organization as the resource <c>ManageMembers</c> is asked against (add, remove).</summary>
public sealed record MemberBindingResource(string UserId, string OrganizationId)
{
    public override string ToString() => $"binding of {UserId} to {OrganizationId}";
}

/// <summary>
/// Members' resource mapper (docs/access.md, Domains onto the extension system): decides
/// <c>ManageMembers</c> against an organization or a binding instead of one global check. Staff
/// (and applications) answer with the tenant-wide permission. A caller acting in an
/// organization (the member side, an organization system actor) answers only for its own
/// organization, with the permission its binding's role template grants there, and for a
/// binding only when the user is already within that organization's hierarchy root (the root
/// guard bounds the who; the template the what). Any other organization is denied.
/// </summary>
public sealed class MemberResourcePermissionMapper(ISession session) : IResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (permission != MembersPermissions.ManageMembers.Name)
        {
            return null;
        }

        var (organizationId, userId) = resource switch
        {
            MemberOrganizationResource organization => (organization.OrganizationId, (string?)null),
            MemberBindingResource binding => (binding.OrganizationId, binding.UserId),
            _ => (null, null),
        };

        if (organizationId is null)
        {
            return null;
        }

        if (!ActsInOrganization(caller))
        {
            return [permission];
        }

        if (!string.Equals(caller.OrganizationId, organizationId, StringComparison.Ordinal))
        {
            return [];
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
