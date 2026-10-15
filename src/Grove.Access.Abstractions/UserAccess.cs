namespace Grove.Access;

/// <summary>
/// Where a grant applies (docs/permissions.md, the permission model › Reach): the tenant
/// (staff, acting on the admin side over every organization) or one organization (a member
/// acting there on the member side). Admin-side rights never bleed into the member side.
/// </summary>
public readonly record struct AccessReach(string? OrganizationId)
{
    public static readonly AccessReach Tenant = new((string?)null);

    public static AccessReach Organization(string organizationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        return new AccessReach(organizationId);
    }

    public bool IsTenant => OrganizationId is null;

    /// <summary>The reach as stored in index columns: the organization id, or empty for the tenant.</summary>
    public string Key => OrganizationId ?? string.Empty;

    public static AccessReach FromKey(string? key) => string.IsNullOrEmpty(key) ? Tenant : new AccessReach(key);

    public override string ToString() => IsTenant ? "tenant" : $"organization {OrganizationId}";
}

/// <summary>The level a per-user override speaks at: the most specific level that mentions a permission decides it.</summary>
public enum OverrideLevel
{
    Role = 0,
    Set = 1,
    Permission = 2,
}

public enum OverrideEffect
{
    Grant = 0,
    Deny = 1,
}

/// <summary>One per-user override within a reach: a role, a set or a permission, granted or denied.</summary>
public sealed record UserAccessOverride(OverrideLevel Level, string Name, OverrideEffect Effect);

/// <summary>A grant and a deny of one name at one level for one user and reach: refused at write time.</summary>
public sealed class AccessOverrideConflictException(OverrideLevel level, string name)
    : InvalidOperationException($"'{name}' is both granted and denied at the {level.ToString().ToLowerInvariant()} level; one override per name and level.")
{
    public OverrideLevel Level { get; } = level;

    public string Name { get; } = name;
}

/// <summary>Where one effective permission came from, so an editor can explain itself.</summary>
public sealed record UserAccessGrant(string Permission, string Source)
{
    public const string OverrideSource = "override";

    public static string RoleSource(string role) => $"role:{role}";

    public static string SetSource(string set, string role) => $"set:{set} via role:{role}";
}

/// <summary>
/// Everything one user holds at one reach, resolved (docs/permissions.md › Projection): the
/// groups, the roles the groups and the direct assignment give (role overrides applied), the
/// sets those roles hold (set overrides applied) and the final permissions with their
/// provenance (permission overrides applied). Built under a permission version.
/// </summary>
public sealed record UserAccess(
    string UserId,
    AccessReach Reach,
    IReadOnlySet<string> Groups,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Sets,
    IReadOnlyList<UserAccessGrant> Permissions,
    long PermissionVersion)
{
    public IReadOnlySet<string> PermissionNames => new HashSet<string>(Permissions.Select(grant => grant.Permission), StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The projection the authorization hot path reads: a user's access at a reach, rebuilt
/// under the current permission version when it is missing or stale. Null for an unknown user.
/// </summary>
public interface IUserAccessReader
{
    Task<UserAccess?> ReadAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default);
}

/// <summary>Rebuilds the projection when a relation touching a user changed.</summary>
public interface IUserAccessProjector
{
    /// <summary>Rebuilds every reach of one user.</summary>
    Task RebuildUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Rebuilds every user's projection (a migration, a repair).</summary>
    Task<int> RebuildAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>A group: users bound to roles within a reach (docs/permissions.md › Layers).</summary>
public sealed record AccessGroupModel(
    string Id,
    string Name,
    string? Description,
    AccessReach Reach,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> UserIds,
    // For an organization's group cloned from a tenant group: that group's id.
    string? TemplateId);

public sealed record AccessGroupWrite(string Name, string? Description, IReadOnlyList<string> Roles);

/// <summary>
/// The one group store. Tenant groups are the tenant's; an organization's groups are cloned
/// from the tenant's allow-listed groups when the organization is created and are then the
/// organization's own (edited by its admins within the tenant's bounds).
/// </summary>
public interface IAccessGroupService
{
    Task<IReadOnlyList<AccessGroupModel>> ListAsync(AccessReach reach, CancellationToken cancellationToken = default);

    Task<AccessGroupModel?> GetAsync(string groupId, CancellationToken cancellationToken = default);

    Task<AccessGroupModel> CreateAsync(AccessReach reach, AccessGroupWrite write, CancellationToken cancellationToken = default);

    Task<AccessGroupModel?> UpdateAsync(string groupId, AccessGroupWrite write, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string groupId, CancellationToken cancellationToken = default);

    Task AddUserAsync(string groupId, string userId, CancellationToken cancellationToken = default);

    Task RemoveUserAsync(string groupId, string userId, CancellationToken cancellationToken = default);

    /// <summary>The groups a user is in at a reach.</summary>
    Task<IReadOnlyList<AccessGroupModel>> GroupsOfAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a new organization its default groups: a clone of every allow-listed tenant group
    /// (<see cref="OrganizationAccessDefaults.DefaultGroupIds"/>) it does not have yet, with
    /// the roles, without the users. Idempotent by template.
    /// </summary>
    Task<IReadOnlyList<AccessGroupModel>> CloneDefaultsAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from every group of a reach (a member leaving an organization).</summary>
    Task RemoveUserFromReachAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default);

    /// <summary>Removes an organization's groups and overrides (the organization is gone).</summary>
    Task RemoveOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);
}

/// <summary>A user's overrides at a reach; the source of truth the projection flattens.</summary>
public interface IUserAccessOverrideService
{
    Task<IReadOnlyList<UserAccessOverride>> GetAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default);

    /// <summary>Replaces the user's overrides at the reach; throws <see cref="AccessOverrideConflictException"/> on a same-level conflict.</summary>
    Task SetAsync(string userId, AccessReach reach, IReadOnlyList<UserAccessOverride> overrides, CancellationToken cancellationToken = default);
}

/// <summary>
/// The tenant's organization defaults (docs/permissions.md › Reach, tenant-wide settings):
/// the tenant groups a new organization is cloned with, the roles its admins may put in its
/// groups, and the permissions only the organization's owner account may hold.
/// </summary>
public class OrganizationAccessDefaults
{
    public List<string> DefaultGroupIds { get; set; } = [];

    public List<string> AssignableRoles { get; set; } = [];

    public List<string> OwnerLockedPermissions { get; set; } = [];
}

public interface IOrganizationAccessDefaultsService
{
    Task<OrganizationAccessDefaults> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(OrganizationAccessDefaults defaults, CancellationToken cancellationToken = default);
}

/// <summary>
/// An organization as the resource an organization permission is asked against
/// (<c>ManageOrgMembers</c> for its members and contacts, its groups): the domain's mapper
/// answers for the caller's own organization, staff for every one.
/// </summary>
public sealed record OrganizationResource(string OrganizationId)
{
    public override string ToString() => $"organization {OrganizationId}";
}

/// <summary>
/// The two organization permissions (docs/access.md § Members, ruling 2026-10-13), by name so
/// every domain that maps them (Members, Parties) can without referencing the module that
/// defines them. Neither is on the member ceiling: staff hold them at tenant reach and reach
/// every organization; a member holds them through an organization's group and reaches that
/// organization only. The same permission, a different reach.
/// </summary>
public static class OrganizationPermissionNames
{
    /// <summary>Every member: their own Person, contact points and positions in the organizations they are bound to.</summary>
    public const string ManageOwnOrgMember = "ManageOwnOrgMember";

    /// <summary>An organization's members and contacts.</summary>
    public const string ManageOrgMembers = "ManageOrgMembers";
}

/// <summary>
/// One user within an organization, as the resource <c>ManageOrgMembers</c> is asked against
/// when the user is put in the organization's groups or given overrides there: the domain's
/// mapper adds the root guard (the user must be a member of that organization).
/// </summary>
public sealed record OrganizationMemberResource(string OrganizationId, string UserId)
{
    public override string ToString() => $"member {UserId} of {OrganizationId}";
}

/// <summary>
/// Who may manage which users (docs/permissions.md › Who manages the permission model): the
/// user hierarchy bounds every write to a user's overrides and group memberships, and the user
/// lists the editors show. A manager manages the users under them in the hierarchy of the
/// reach (an organization's own; the tenant organization's for the tenant reach); there is no
/// managing without a position, and the super user is not narrowed. Implemented by the module
/// that owns the hierarchy; with none registered, only the super user manages users.
/// </summary>
public interface IUserManagementReach
{
    /// <summary>The users <paramref name="caller"/> manages at <paramref name="reach"/> (for the super user, every user of the reach).</summary>
    Task<IReadOnlySet<string>?> ManagedUsersAsync(CallerContext caller, AccessReach reach, CancellationToken cancellationToken = default);
}

public static class UserManagementReachExtensions
{
    public static async Task<bool> ManagesAsync(this IUserManagementReach? reach, CallerContext? caller, AccessReach at, string userId, CancellationToken cancellationToken = default)
    {
        if (caller is null)
        {
            return false;
        }

        if (caller.IsSuperUser)
        {
            return true;
        }

        if (reach is null)
        {
            return false;
        }

        var managed = await reach.ManagedUsersAsync(caller, at, cancellationToken);
        return managed is null || managed.Contains(userId);
    }
}

/// <summary>
/// The organization owner (docs/permissions.md › The organization owner): one user account
/// per organization, recorded on it; always an org-admin; the only holder of the owner-locked
/// permissions there. The first member bound to an organization becomes its owner; ownership
/// moves only by transfer. Implemented by Members; other domains (Parties' portal promotion)
/// resolve it when it is registered.
/// </summary>
public interface IOrganizationOwnership
{
    /// <summary>The role the owner always holds in the organization (its org-admin role).</summary>
    string OwnerRoleName { get; }

    Task<string?> GetOwnerAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Records <paramref name="userId"/> as the owner when the organization has none; true when they became it.</summary>
    Task<bool> EnsureOwnerAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves ownership to <paramref name="newOwnerUserId"/>, a user of the tenant: bound to the
    /// organization as a member when they are not, made an org-admin, recorded as owner, and
    /// audited. The previous owner keeps their binding and org-admin role, unprotected.
    /// Authorization is the caller's (staff with the tenant permission, or the current owner).
    /// </summary>
    Task TransferAsync(string organizationId, string newOwnerUserId, CancellationToken cancellationToken = default);
}

/// <summary>A write that would take the owner's org-admin role away; refused whoever asks.</summary>
public sealed class OwnerProtectionException(string message) : InvalidOperationException(message);
