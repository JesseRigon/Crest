using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.Users.Localization.ViewModels;

public class UserLocalizationViewModel
{
    public string SelectedCulture { get; set; }

    [BindNever]
    public List<SelectListItem> CultureList { get; set; } = [];
}
