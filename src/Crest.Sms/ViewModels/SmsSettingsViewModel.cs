using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.Sms.ViewModels;

public class SmsSettingsViewModel : SmsSettingsBaseViewModel
{
    [BindNever]
    public SelectListItem[] Providers { get; set; }
}
