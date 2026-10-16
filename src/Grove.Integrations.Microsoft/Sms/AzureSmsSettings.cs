using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Microsoft.Sms;

/// <summary>
/// The Azure Communication Services provider's section of Settings › SMS. Holds no secret: the
/// connection string is the connection's credential (the Connections screen).
/// </summary>
public class AzureSmsSettings
{
    [Display(Name = "Enable", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Phone Number", Description = "The default phone number to use as a sender. Phone number must include a country code. For example, +1 for United States.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string PhoneNumber { get; set; }

    [Display(Name = "Connection", Description = "Its connection string is set on the Connections screen.", Prompt = "Select a connection", Order = 2)]
    [Setting(Options = typeof(AzureSmsConnectionOptions), VisibleWhen = nameof(IsEnabled))]
    public string ConnectionKey { get; set; } = AzureCommunicationServices.SmsConnectionKey;
}
