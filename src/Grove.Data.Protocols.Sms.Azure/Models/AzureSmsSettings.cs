using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;
using Grove.Data.Protocols.Sms.Azure.Services;

namespace Grove.Data.Protocols.Sms.Azure.Models;

public class AzureSmsSettings
{
    [Display(Name = "Enable", Order = 0)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Connection String", Order = 2)]
    [Setting(Secret = true, ProtectedBy = AzureSmsOptionsConfiguration.ProtectorName, VisibleWhen = nameof(IsEnabled))]
    public string ConnectionString { get; set; }

    [Display(Name = "Phone Number", Description = "The default phone number to use as a sender. Phone number must include a country code. For example, +1 for United States.", Order = 1)]
    [Setting(VisibleWhen = nameof(IsEnabled))]
    public string PhoneNumber { get; set; }
}
