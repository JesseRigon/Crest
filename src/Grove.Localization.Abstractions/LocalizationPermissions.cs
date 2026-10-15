using Grove.Security.Permissions;

namespace Grove.Localization;

public static class LocalizationPermissions
{
    public static readonly Permission ManageCultures = new("ManageCultures", "Manage supported culture");
}
