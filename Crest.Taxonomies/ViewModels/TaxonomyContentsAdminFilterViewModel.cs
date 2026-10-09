using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.Taxonomies.ViewModels;

public class TaxonomyContentsAdminFilterViewModel
{
    public string SelectedContentItemId { get; set; }

    [BindNever]
    public string DisplayText { get; set; }

    [BindNever]
    public List<SelectListItem> Taxonomies { get; set; }
}
