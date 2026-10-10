using Crest.Security.Permissions;

namespace Crest.UrlRewriting;

public static class UrlRewritingPermissions
{
    public static readonly Permission ManageUrlRewritingRules = new Permission("ManageUrlRewritingRules", "Manage URLs rewriting rules");
}
