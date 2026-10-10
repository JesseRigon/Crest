using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Sms.ViewModels;

namespace Crest.Sms.Azure.ViewModels;

public class AzureSettingsViewModel : SmsSettingsBaseViewModel
{
    public bool IsEnabled { get; set; }

    public string ConnectionString { get; set; }

    public string PhoneNumber { get; set; }

    [BindNever]
    public bool HasConnectionString { get; set; }
}
