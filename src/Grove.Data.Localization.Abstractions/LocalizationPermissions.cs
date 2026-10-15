using Grove.Security.Permissions;

namespace Grove.Data.Localization;

public static class LocalizationPermissions
{
    public static readonly Permission ManageCultures = new("ManageCultures", "Manage supported culture");
}
