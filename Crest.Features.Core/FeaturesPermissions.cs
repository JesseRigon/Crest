using Crest.Security.Permissions;

namespace Crest.Features;

public static class FeaturesPermissions
{
    public static readonly Permission ManageFeatures = new("ManageFeatures", "Manage Features");
}
