using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Twilio.Models;

/// <summary>
/// The Twilio provider's section of Settings › SMS. Holds no secret: the credential is the
/// connection's (the Connections screen).
/// </summary>
public class TwilioSettings
{
    [Display(Name = "Enable Twilio Provider", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Phone Number", Description = "Phone number must include a country code. For example, +1 for United States.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string PhoneNumber { get; set; }

    [Display(Name = "Account SID", Description = "The account messages are created under (the connection may sign in with an API key of it).", Order = 2)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string AccountSID { get; set; }

    [Display(Name = "Connection", Description = "The Twilio connection messages are sent through; its credential is set on the Connections screen.", Prompt = "Select a connection", Order = 3)]
    [Setting(Options = typeof(TwilioConnectionSettingOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; } = TwilioConnection.Key;
}
