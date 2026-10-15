using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Communications.Sms.Models;

public class TwilioSettings
{
    /// <summary>The data protection purpose of <see cref="AuthToken"/> (the Twilio provider's protector).</summary>
    public const string AuthTokenProtectorName = "Twilio";

    [Display(Name = "Enable Twilio Provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Phone Number", Description = "Phone number must include a country code. For example, +1 for United States.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string PhoneNumber { get; set; }

    [Display(Name = "Account SID", Order = 2)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string AccountSID { get; set; }

    [Display(Name = "Auth Token", Order = 3)]
    [Setting(Secret = true, ProtectedBy = AuthTokenProtectorName, VisibleWhen = nameof(IsEnabled))]
    public string AuthToken { get; set; }
}
