using Grove.Security.Permissions;

namespace Grove.Sitemaps;

public static class SitemapsPermissions
{
    public static readonly Permission ManageSitemaps = new("ManageSitemaps", "Manage sitemaps");
}
