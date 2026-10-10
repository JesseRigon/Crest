using Microsoft.AspNetCore.Mvc.Rendering;

namespace Crest.Indexing.Deployments.ViewModels;

public class IndexProfileDeploymentStepViewModel
{
    public bool IncludeAll { get; set; }

    public SelectListItem[] Indexes { get; set; }
}
