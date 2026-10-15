using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grove.Sms.ViewModels;

public class SmsSettingsViewModel : SmsSettingsBaseViewModel
{
    [BindNever]
    public SelectListItem[] Providers { get; set; }
}
