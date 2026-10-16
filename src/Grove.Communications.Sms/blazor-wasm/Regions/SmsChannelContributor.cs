using Grove.Communications.Sms.BlazorWasm.Pages;
using Grove.Display.Components.Regions;

namespace Grove.Communications.Sms.BlazorWasm.Regions;

/// <summary>Puts the SMS channel's card on the Communications page.</summary>
public sealed class SmsChannelContributor : IPageRegionContributor
{
    /// <summary>Grove.Communications' channels region (<c>CommunicationsRegions.Channels</c>).</summary>
    public string RegionKey => "Communications.Channels";

    public int Order => 20;

    public Type ComponentType => typeof(SmsChannelCard);
}
