using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.Facebook.Settings;

public class FacebookPixelSettings
{
    [Display(Name = "Pixel Identifier", Description = "The Pixel ID found on the Meta Data Sources portal.", Order = 0)]
    public string PixelId { get; set; }

    /// <summary>
    /// The Meta Conversions API access token, generated from the Events Manager for the pixel's
    /// dataset. Stored encrypted via <see cref="Microsoft.AspNetCore.DataProtection.IDataProtectionProvider"/>.
    /// </summary>
    [Display(Name = "Conversions API Access Token", Description = "The access token generated for this dataset in the Events Manager's Conversions API settings. Optional server-side event delivery to Meta, in addition to the browser-side pixel: configure it to let workflows send events (e.g. Purchase, Lead) directly from the server.", GroupName = "Conversions API", Order = 1)]
    [Setting(Secret = true, ProtectedBy = FacebookConstants.ConversionsApiProtectorName)]
    public string ConversionsApiAccessToken { get; set; }

    /// <summary>
    /// Optional test event code from the Events Manager "Test Events" tab, sent with every
    /// Conversions API request so events show up in the test console instead of production data.
    /// </summary>
    [Display(Name = "Test Event Code", Description = "Optional. Found on the Events Manager's Test Events tab. When set, every Conversions API event includes it, so events show up in the test console instead of counting as production data. Remove it before going live.", GroupName = "Conversions API", Order = 2)]
    public string ConversionsApiTestEventCode { get; set; }
}
