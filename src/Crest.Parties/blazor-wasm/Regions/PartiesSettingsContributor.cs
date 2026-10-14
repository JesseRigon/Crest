using Crest.Components.Regions;
using Crest.Parties.BlazorWasm.Components;

namespace Crest.Parties.BlazorWasm.Regions;

/// <summary>Puts the Parties settings section on the general settings page.</summary>
public sealed class PartiesSettingsContributor : IPageRegionContributor
{
    public string RegionKey => SettingsPageRegions.General;
    public int Order => 100;
    public Type ComponentType => typeof(PartiesSettingsSection);
}
