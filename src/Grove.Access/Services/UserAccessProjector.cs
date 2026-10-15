using Grove.Access.Store;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Access.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace Grove.Access.Services;

/// <summary>
/// Builds and reads the projection (docs/permissions.md › Projection). A read returns the
/// stored projection when it was built under the current permission version, else resolves
/// the user's access from the relation stores (groups, the user's roles, the roles' sets and
/// loose permissions, the overrides), stores it and returns it. The relations stay the source
/// of truth; the projection is rebuilt, never edited.
/// </summary>
public sealed class UserAccessProjector(
    ISession session,
    IServiceProvider services,
    RoleManager<IRole> roleManager,
    IPermissionSetService permissionSets,
    IPermissionVersion permissionVersion) : IUserAccessReader, IUserAccessProjector
{
    // Resolved when used, never in the constructor: the user store's event handlers reach the
    // group service (Members places a new user in organization groups), which rebuilds through
    // this projector, so a constructor dependency on the user manager is a cycle.
    private UserManager<IUser> UserManager => services.GetRequiredService<UserManager<IUser>>();

    public async Task<UserAccess?> ReadAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var version = await permissionVersion.GetAsync(cancellationToken);
        var stored = await FindAsync(userId, reach, cancellationToken);
        if (stored is not null && stored.PermissionVersion == version)
        {
            return ToAccess(stored);
        }

        if (await UserManager.FindByIdAsync(userId) is not { } user)
        {
            return null;
        }

        var access = await ResolveAsync(user, reach, version, cancellationToken);
        await StoreAsync(stored, access, cancellationToken);
        return access;
    }

    public async Task RebuildUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await UserManager.FindByIdAsync(userId) is not { } user)
        {
            return;
        }

        var version = await permissionVersion.GetAsync(cancellationToken);
        var reaches = new HashSet<string>(StringComparer.Ordinal) { AccessReach.Tenant.Key };
        foreach (var projection in await session.Query<UserAccessProjection, UserAccessIndex>(index => index.UserId == userId).ListAsync(cancellationToken))
        {
            reaches.Add(projection.OrganizationId);
        }

        foreach (var membership in await session.QueryIndex<AccessGroupMemberIndex>(index => index.UserId == userId).ListAsync(cancellationToken))
        {
            reaches.Add(membership.OrganizationId);
        }

        foreach (var key in reaches)
        {
            var reach = AccessReach.FromKey(key);
            await StoreAsync(await FindAsync(userId, reach, cancellationToken), await ResolveAsync(user, reach, version, cancellationToken), cancellationToken);
        }
    }

    public async Task<int> RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var user in UserManager.Users.ToList())
        {
            if (user is Grove.Access.Users.Models.User { UserId: { Length: > 0 } userId })
            {
                await RebuildUserAsync(userId, cancellationToken);
                count++;
            }
        }

        return count;
    }

    private async Task<UserAccess> ResolveAsync(IUser user, AccessReach reach, long version, CancellationToken cancellationToken)
    {
        var userId = (user as Grove.Access.Users.Models.User)?.UserId ?? await UserManager.GetUserIdAsync(user);
        // Direct role assignments are tenant reach: an organization's roles come from its groups only.
        var directRoles = reach.IsTenant ? (await UserManager.GetRolesAsync(user)).ToArray() : [];
        // Read from the relation stores directly (the group and override services rebuild
        // through this projector, so it never depends on them).
        var key = reach.Key;
        var userGroups = (await session.Query<AccessGroup, AccessGroupMemberIndex>(index => index.UserId == userId && index.OrganizationId == key).ListAsync(cancellationToken))
            .Select(group => new AccessGroupModel(group.GroupId, group.Name, group.Description, reach, group.Roles, group.UserIds, group.TemplateId))
            .ToArray();
        var userOverrides = (await session.Query<UserAccessOverrides, UserAccessOverrideIndex>(index => index.UserId == userId && index.OrganizationId == key).FirstOrDefaultAsync(cancellationToken))?.Entries.ToArray() ?? [];

        var roles = new Dictionary<string, RoleDefinition?>(StringComparer.OrdinalIgnoreCase);
        var sets = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in await permissionSets.ListAsync(cancellationToken))
        {
            sets[set.Name] = set.Permissions;
        }

        // Every role the resolution can reach, read up front (the role document is cached).
        foreach (var roleName in userGroups.SelectMany(group => group.Roles).Concat(directRoles).Concat(userOverrides.Where(entry => entry.Level == OverrideLevel.Role && entry.Effect == OverrideEffect.Grant).Select(entry => entry.Name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            roles[roleName] = await roleManager.FindByNameAsync(roleName) is Role role
                ? new RoleDefinition(role.RoleName, role.PermissionSets, role.RoleClaims.Where(claim => claim.ClaimType == Permission.ClaimType).Select(claim => claim.ClaimValue).ToArray())
                : null;
        }

        return AccessResolution.Resolve(userId, reach, version, new AccessResolutionInput(
            userGroups,
            directRoles,
            userOverrides,
            name => roles.GetValueOrDefault(name),
            name => sets.GetValueOrDefault(name)));
    }

    private async Task StoreAsync(UserAccessProjection? existing, UserAccess access, CancellationToken cancellationToken)
    {
        var projection = existing ?? new UserAccessProjection { UserId = access.UserId, OrganizationId = access.Reach.Key };
        projection.PermissionVersion = access.PermissionVersion;
        projection.Groups = [.. access.Groups];
        projection.Roles = [.. access.Roles];
        projection.Sets = [.. access.Sets];
        projection.Permissions = [.. access.Permissions];
        await session.SaveAsync(projection);
    }

    private Task<UserAccessProjection?> FindAsync(string userId, AccessReach reach, CancellationToken cancellationToken)
    {
        var key = reach.Key;
        return session.Query<UserAccessProjection, UserAccessIndex>(index => index.UserId == userId && index.OrganizationId == key).FirstOrDefaultAsync(cancellationToken)!;
    }

    private static UserAccess ToAccess(UserAccessProjection projection) => new(
        projection.UserId,
        AccessReach.FromKey(projection.OrganizationId),
        new HashSet<string>(projection.Groups, StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(projection.Roles, StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(projection.Sets, StringComparer.OrdinalIgnoreCase),
        projection.Permissions,
        projection.PermissionVersion);
}

/// <summary>
/// The tenant's organization-assignable list as a ceiling (docs/permissions.md › Overrides,
/// "ceilings are not overrides"): a caller acting in an organization never holds a permission
/// none of the assignable roles grants, whatever its groups or overrides say. An empty list
/// is an unconfigured tenant and bounds nothing.
/// </summary>
public sealed class OrganizationAssignableRolesCeiling(
    IOrganizationAccessDefaultsService defaults,
    RoleManager<IRole> roleManager) : IAccessCeiling
{
    private HashSet<string>? _allowed;
    private bool _loaded;

    public string? Deny(CallerContext caller, string permission, object? resource = null)
    {
        if (caller.OrganizationId is null || caller.IsSuperUser)
        {
            return null;
        }

        if (!_loaded)
        {
            _loaded = true;
            var assignable = defaults.GetAsync().GetAwaiter().GetResult().AssignableRoles;
            if (assignable.Count > 0)
            {
                _allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var roleName in assignable)
                {
                    if (roleManager.FindByNameAsync(roleName).GetAwaiter().GetResult() is { } role)
                    {
                        _allowed.UnionWith(roleManager.GetClaimsAsync(role).GetAwaiter().GetResult().Where(claim => claim.Type == Permission.ClaimType).Select(claim => claim.Value));
                    }
                }
            }
        }

        return _allowed is not null && !_allowed.Contains(permission)
            ? $"'{permission}' is outside the roles the tenant lets an organization assign."
            : null;
    }
}
