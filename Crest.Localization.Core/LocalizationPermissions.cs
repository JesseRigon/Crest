using Crest.Security.Permissions;

namespace Crest.Localization;

public static class LocalizationPermissions
{
    public static readonly Permission ManageCultures = new("ManageCultures", "Manage supported culture");
}
