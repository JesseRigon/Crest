using Crest.Security.Permissions;

namespace Crest.Search;

public static class SearchPermissions
{
    public static readonly Permission ManageSearchSettings = new("ManageSearchSettings", "Manage Search Settings");
}
