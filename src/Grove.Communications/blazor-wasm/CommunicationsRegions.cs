using Grove.Display.Management.Components;

[assembly: ZoneKey(Grove.Communications.BlazorWasm.CommunicationsRegions.Channels, DisplayName = "Communications channels")]

namespace Grove.Communications.BlazorWasm;

/// <summary>
/// The regions of the communications screens. Each channel contributes its card to
/// <see cref="Channels"/> (an <c>IPageRegionContributor</c> in the channel's own WASM library),
/// so Communications never references a channel.
/// </summary>
public static class CommunicationsRegions
{
    public const string Channels = "Communications.Channels";
}
