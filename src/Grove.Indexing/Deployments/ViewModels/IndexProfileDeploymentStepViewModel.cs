using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grove.Indexing.Deployments.ViewModels;

public class IndexProfileDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public SelectListItem[] Indexes { get; set; }
}
