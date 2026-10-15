using Grove.Security.Permissions;

namespace Grove.Infrastructure.Shells.Admin;

public static class AdminPermissions
{
    public static readonly Permission AccessAdminPanel = new("AccessAdminPanel", "Access admin panel");

    public static readonly Permission ManageAdminSettings = new("ManageAdminSettings", "Manage Admin Settings");
}
