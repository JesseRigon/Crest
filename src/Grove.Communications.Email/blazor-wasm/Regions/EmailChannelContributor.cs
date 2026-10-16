using Grove.Communications.Email.BlazorWasm.Pages;
using Grove.Display.Components.Regions;

namespace Grove.Communications.Email.BlazorWasm.Regions;

/// <summary>Puts the e-mail channel's card on the Communications page.</summary>
public sealed class EmailChannelContributor : IPageRegionContributor
{
    /// <summary>Grove.Communications' channels region (<c>CommunicationsRegions.Channels</c>).</summary>
    public string RegionKey => "Communications.Channels";

    public int Order => 10;

    public Type ComponentType => typeof(EmailChannelCard);
}
