using Grove.DisplayManagement.Components;

[assembly: ZoneKey(Grove.Components.Regions.SettingsPageRegions.General, DisplayName = "General settings sections")]

namespace Grove.Components.Regions;

/// <summary>
/// Region keys the admin settings pages open for modules' own tenant-setting sections (the
/// page-region seam, docs/page-regions.md): a module the admin theme must not depend on
/// renders its settings section under the general settings. Each contributed section saves
/// its own settings through its own API and hides itself when the module's API is absent.
/// </summary>
public static class SettingsPageRegions
{
    public const string General = "Settings.General";
}
