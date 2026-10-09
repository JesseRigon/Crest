using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Crest.Forms.Models;

namespace Crest.Forms.ViewModels;

public class FormInputElementVisibilityViewModel
{
    public FormVisibilityAction Action { get; set; }

    public IEnumerable<FormVisibilityRuleGroupViewModel> Groups { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Actions { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Operators { get; set; }
}
