using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Facebook.Settings;

/// <summary>
/// The Meta Pixel. Server-side events go through the Meta Conversions API connection, whose
/// tenant credential is the dataset's access token (Connections screen).
/// </summary>
public class FacebookPixelSettings
{
    [Display(Name = "Pixel Identifier", Description = "The Pixel ID found on the Meta Data Sources portal.", Order = 0)]
    public string PixelId { get; set; }

    /// <summary>
    /// Optional test event code from the Events Manager "Test Events" tab, sent with every
    /// Conversions API request so events show up in the test console instead of production data.
    /// </summary>
    [Display(Name = "Test Event Code", Description = "Optional. Found on the Events Manager's Test Events tab. When set, every Conversions API event includes it, so events show up in the test console instead of counting as production data. Remove it before going live.", GroupName = "Conversions API", Order = 2)]
    public string ConversionsApiTestEventCode { get; set; }
}
