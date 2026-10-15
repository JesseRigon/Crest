using Grove.Components.Regions;
using Grove.Parties.BlazorWasm.Components;

namespace Grove.Parties.BlazorWasm.Regions;

/// <summary>Puts the Parties settings section on the general settings page.</summary>
public sealed class PartiesSettingsContributor : IPageRegionContributor
{
    public string RegionKey => SettingsPageRegions.General;
    public int Order => 100;
    public Type ComponentType => typeof(PartiesSettingsSection);
}
