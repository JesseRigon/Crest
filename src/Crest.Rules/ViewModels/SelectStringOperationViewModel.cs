using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.Rules.ViewModels;

public class SelectStringOperationViewModel
{
    public string HtmlName { get; set; }
    public List<SelectListItem> Items { get; set; }
}
