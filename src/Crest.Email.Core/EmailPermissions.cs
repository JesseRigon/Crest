using Crest.Security.Permissions;

namespace Crest.Email;

public static class EmailPermissions
{
    public static readonly Permission ManageEmailSettings = new("ManageEmailSettings", "Manage Email Settings");
}
