using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Communications.Email;

/// <summary>
/// The e-mail channel's settings: the tenant's default send and receive providers. Each
/// provider adds its own section to the same settings group (<see cref="GroupId"/>).
/// </summary>
public class EmailSettings
{
    /// <summary>The settings group of the e-mail channel (Settings › Email).</summary>
    public const string GroupId = "email";

    /// <summary>The technical name of the provider messages are sent through by default.</summary>
    [Display(Name = "Default email provider", Prompt = "Select a provider", GroupName = "Providers", Order = 0)]
    [Setting(Options = typeof(EmailProviderSettingOptions))]
    public string DefaultProviderName { get; set; }

    /// <summary>The technical name of the provider mail is received through by default.</summary>
    [Display(Name = "Default receive provider", Prompt = "Select a provider", GroupName = "Providers", Order = 1)]
    [Setting(Options = typeof(EmailReceiveProviderSettingOptions))]
    public string DefaultReceiveProviderName { get; set; }
}
