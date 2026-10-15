using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.AzureAI.ViewModels;

public class RebuildIndexDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public string[] IndexNames { get; set; }

    [BindNever]
    public string[] AllIndexNames { get; set; }
}
