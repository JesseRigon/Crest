using Grove.Security.Permissions;

namespace Grove.Infrastructure.Features;

public static class FeaturesPermissions
{
    public static readonly Permission ManageFeatures = new("ManageFeatures", "Manage Features");
}
