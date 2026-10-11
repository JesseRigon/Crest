using Crest.Access;
using Crest.Security.Services;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Users.Models;

namespace Crest.Users.Services;

/// <summary>
/// User and role permissions asked against a role or a user: the per-role variation
/// (<c>ListUsersInRole_{role}</c>, ...) for the role, or for each of the user's roles (the
/// assignable roles when the user holds none); <c>EditOwnUser</c> for the caller's own
/// account. The base permission always counts too.
/// </summary>
public sealed class RolePermissionMapper(IRoleService roleService) : IResourcePermissionMapper
{
    public async ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        switch (resource)
        {
            case IRole role:
            {
                var candidates = new List<PermissionCandidate> { permission };
                if (Variation(permission, role.RoleName) is { } variant)
                {
                    candidates.Add(variant.Name);
                }

                return candidates;
            }

            case User user:
            {
                var candidates = new List<PermissionCandidate> { permission };
                if (permission == UsersPermissions.EditUsers.Name && caller.UserId is not null && string.Equals(user.UserId, caller.UserId, StringComparison.Ordinal))
                {
                    candidates.Add(UsersPermissions.EditOwnUser.Name);
                }

                IEnumerable<string> roleNames = user.RoleNames ?? [];
                if (!roleNames.Any())
                {
                    // A user in no roles: the caller may act when they may manage any assignable role.
                    roleNames = (await roleService.GetAssignableRolesAsync()).Select(r => r.RoleName);
                }

                foreach (var roleName in roleNames)
                {
                    if (Variation(permission, roleName) is { } variant)
                    {
                        candidates.Add(variant.Name);
                    }
                }

                return candidates;
            }

            default:
                return null;
        }
    }

    private static Permission? Variation(string permission, string roleName)
    {
        if (permission == UsersPermissions.ListUsers.Name)
        {
            return UsersPermissions.CreateListUsersInRolePermission(roleName);
        }

        if (permission == UsersPermissions.EditUsers.Name)
        {
            return UsersPermissions.CreateEditUsersInRolePermission(roleName);
        }

        if (permission == UsersPermissions.DeleteUsers.Name)
        {
            return UsersPermissions.CreateDeleteUsersInRolePermission(roleName);
        }

        if (permission == UsersPermissions.AssignRoleToUsers.Name)
        {
            return UsersPermissions.CreateAssignRoleToUsersPermission(roleName);
        }

        if (permission == UsersPermissions.ManageUsers.Name)
        {
            return UsersPermissions.CreatePermissionForManageUsersInRole(roleName);
        }

        return null;
    }
}
