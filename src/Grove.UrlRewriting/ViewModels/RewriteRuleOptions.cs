using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grove.UrlRewriting.ViewModels;

public class RewriteRuleOptions
{
    public string Search { get; set; }

    public RewriteRuleAction BulkAction { get; set; }

    [BindNever]
    public List<SelectListItem> BulkActions { get; set; }
}
