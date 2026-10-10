using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.AzureAI.ViewModels;

public class AzureAISettingsIndexProfileViewModel
{
    public string AnalyzerName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Analyzers { get; set; }
}
