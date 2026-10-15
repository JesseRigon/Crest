using Grove.Security.Permissions;

namespace Grove.Roles;

public static class RolesPermissions
{
    public static readonly Permission ManageRoles = new("ManageRoles", "Manage Roles", isSecurityCritical: true);
    public static readonly Permission ViewRoles = new("ViewRoles", "View Roles", [ManageRoles]);
}
