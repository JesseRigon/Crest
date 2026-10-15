using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Communications.Sms;

public class SmsSettings
{
    /// <summary>
    /// The group identifier for configuring drivers.
    /// </summary>
    public const string GroupId = "sms";

    /// <summary>
    /// The name of the SMS provider to use.
    /// </summary>
    [Display(Name = "Default SMS provider", Prompt = "Select a provider", GroupName = "Providers")]
    [Setting(Options = typeof(SmsProviderSettingOptions))]
    public string DefaultProviderName { get; set; }
}
