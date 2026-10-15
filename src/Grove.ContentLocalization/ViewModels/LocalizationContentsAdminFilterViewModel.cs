using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grove.ContentLocalization.ViewModels;

public class LocalizationContentsAdminFilterViewModel
{
    public bool ShowLocalizedContentTypes { get; set; }
    public string SelectedCulture { get; set; }

    [BindNever]
    public List<SelectListItem> Cultures { get; set; }
}
