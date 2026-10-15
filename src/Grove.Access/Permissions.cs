using Grove.Security;
using Grove.Security.Permissions;

namespace Grove.Access;

/// <summary>
/// Who manages the permission model (docs/permissions.md, ruling 2026-10-13): CRUD
/// permissions over roles, permission sets, user overrides and groups, plus reading the
/// permission catalog. Permissions themselves are not managed: modules bring them.
/// </summary>
public static class AccessPermissions
{
    public static readonly Permission CreateRoles = new("CreateRoles", "Create roles");
    public static readonly Permission ReadRoles = new("ReadRoles", "Read roles");
    public static readonly Permission UpdateRoles = new("UpdateRoles", "Update roles");
    public static readonly Permission DeleteRoles = new("DeleteRoles", "Delete roles");

    public static readonly Permission CreatePermissionSets = new("CreatePermissionSets", "Create permission sets");
    public static readonly Permission ReadPermissionSets = new("ReadPermissionSets", "Read permission sets");
    public static readonly Permission UpdatePermissionSets = new("UpdatePermissionSets", "Update permission sets");
    public static readonly Permission DeletePermissionSets = new("DeletePermissionSets", "Delete permission sets");

    public static readonly Permission ReadPermissionCatalog = new("ReadPermissionCatalog", "Read the permission catalog");

    public static readonly Permission CreateUserOverrides = new("CreateUserOverrides", "Create user permission overrides");
    public static readonly Permission ReadUserOverrides = new("ReadUserOverrides", "Read user permission overrides");
    public static readonly Permission UpdateUserOverrides = new("UpdateUserOverrides", "Update user permission overrides");
    public static readonly Permission DeleteUserOverrides = new("DeleteUserOverrides", "Delete user permission overrides");

    public static readonly Permission CreateGroups = new("CreateGroups", "Create groups");
    public static readonly Permission ReadGroups = new("ReadGroups", "Read groups");
    public static readonly Permission UpdateGroups = new("UpdateGroups", "Update groups");
    public static readonly Permission DeleteGroups = new("DeleteGroups", "Delete groups");

    public static readonly Permission CreateGroupMembership = new("CreateGroupMembership", "Add users to groups");
    public static readonly Permission ReadGroupMembership = new("ReadGroupMembership", "Read group membership");
    public static readonly Permission UpdateGroupMembership = new("UpdateGroupMembership", "Update group membership");
    public static readonly Permission DeleteGroupMembership = new("DeleteGroupMembership", "Remove users from groups");

    public const string ManagePermissionsSet = "Manage permissions";
    public const string ManageGroupsSet = "Manage groups";

    public static readonly Permission[] ManagePermissions =
    [
        CreateRoles, ReadRoles, UpdateRoles, DeleteRoles,
        CreatePermissionSets, ReadPermissionSets, UpdatePermissionSets, DeletePermissionSets,
        ReadPermissionCatalog,
        CreateUserOverrides, ReadUserOverrides, UpdateUserOverrides, DeleteUserOverrides,
    ];

    public static readonly Permission[] ManageGroups =
    [
        CreateGroups, ReadGroups, UpdateGroups, DeleteGroups,
        CreateGroupMembership, ReadGroupMembership, UpdateGroupMembership, DeleteGroupMembership,
    ];
}

public sealed class AccessPermissionProvider : IPermissionProvider
{
    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>([.. AccessPermissions.ManagePermissions, .. AccessPermissions.ManageGroups]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = [.. AccessPermissions.ManagePermissions, .. AccessPermissions.ManageGroups],
        },
    ];
}

/// <summary>The two management sets, shipped as defaults the tenant then owns.</summary>
public sealed class AccessPermissionSetProvider : IPermissionSetProvider
{
    public IEnumerable<PermissionSet> GetDefaultSets() =>
    [
        new PermissionSet
        {
            Name = AccessPermissions.ManagePermissionsSet,
            Description = "Roles, permission sets, the permission catalog and user overrides.",
            Permissions = [.. AccessPermissions.ManagePermissions.Select(permission => permission.Name)],
        },
        new PermissionSet
        {
            Name = AccessPermissions.ManageGroupsSet,
            Description = "Groups and group membership.",
            Permissions = [.. AccessPermissions.ManageGroups.Select(permission => permission.Name)],
        },
    ];
}

/// <summary>
/// The management permissions asked against an organization (its groups, its members'
/// overrides): the same permission at a different reach. A caller acting in no organization
/// holds it at tenant reach for every organization; a caller acting in one holds it there
/// only. Which users within the reach is the hierarchy's (<see cref="IUserManagementReach"/>).
/// </summary>
public sealed class OrganizationReachPermissionMapper : IResourcePermissionMapper
{
    private static readonly HashSet<string> s_claimed = new(
        AccessPermissions.ManagePermissions.Concat(AccessPermissions.ManageGroups).Select(permission => permission.Name),
        StringComparer.OrdinalIgnoreCase);

    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var organizationId = resource switch
        {
            OrganizationResource organization => organization.OrganizationId,
            OrganizationMemberResource member => member.OrganizationId,
            _ => null,
        };

        if (organizationId is null || !s_claimed.Contains(permission))
        {
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        IReadOnlyList<PermissionCandidate> answer = caller.OrganizationId is null || string.Equals(caller.OrganizationId, organizationId, StringComparison.Ordinal)
            ? [permission]
            : [];
        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(answer);
    }
}
