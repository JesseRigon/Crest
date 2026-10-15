using Grove.Security.Permissions;

namespace Grove.Communications.Email;

public static class EmailPermissions
{
    public static readonly Permission ManageEmailSettings = new("ManageEmailSettings", "Manage Email Settings");
}
