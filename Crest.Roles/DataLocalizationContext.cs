namespace Crest.Roles;

public static class DataLocalizationContext
{
    public static string Permission(string groupName = null) => groupName is null
        ? "Permissions"
        : $"Permissions{PlatformConstants.DataLocalizationSeparator}{groupName}";
}
