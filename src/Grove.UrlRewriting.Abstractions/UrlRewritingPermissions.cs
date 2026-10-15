using Grove.Security.Permissions;

namespace Grove.UrlRewriting;

public static class UrlRewritingPermissions
{
    public static readonly Permission ManageUrlRewritingRules = new Permission("ManageUrlRewritingRules", "Manage URLs rewriting rules");
}
