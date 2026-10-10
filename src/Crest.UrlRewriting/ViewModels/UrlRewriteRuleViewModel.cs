using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Crest.UrlRewriting.Models;

namespace Crest.UrlRewriting.ViewModels;

public class UrlRewriteRuleViewModel
{
    public string Pattern { get; set; }

    public string SubstitutionPattern { get; set; }

    public bool IsCaseInsensitive { get; set; }

    public QueryStringPolicy QueryStringPolicy { get; set; }

    public bool SkipFurtherRules { get; set; }

    [BindNever]
    public List<SelectListItem> QueryStringPolicies { get; set; }
}
