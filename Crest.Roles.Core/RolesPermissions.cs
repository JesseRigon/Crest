using Crest.Security.Permissions;

namespace Crest.Roles;

public static class RolesPermissions
{
    public static readonly Permission ManageRoles = new("ManageRoles", "Manage Roles", isSecurityCritical: true);
    public static readonly Permission ViewRoles = new("ViewRoles", "View Roles", [ManageRoles]);
}
