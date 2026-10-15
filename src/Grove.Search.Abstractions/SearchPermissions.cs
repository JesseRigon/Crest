using Grove.Security.Permissions;

namespace Grove.Search;

public static class SearchPermissions
{
    public static readonly Permission ManageSearchSettings = new("ManageSearchSettings", "Manage Search Settings");
}
