using Crest.Environment.Shell;

namespace Crest.Tenants;

public static class ShellSettingsExtensions
{
    public static string[] GetFeatureProfiles(this ShellSettings shellSettings)
    {
        return shellSettings["FeatureProfile"]?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            ?? [];
    }
}
