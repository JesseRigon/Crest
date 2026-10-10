using Crest.Security.Permissions;

namespace Crest.Sitemaps;

public static class SitemapsPermissions
{
    public static readonly Permission ManageSitemaps = new("ManageSitemaps", "Manage sitemaps");
}
