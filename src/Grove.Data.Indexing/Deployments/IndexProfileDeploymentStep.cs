using Microsoft.Extensions.Localization;
using Grove.Deployment;
using Grove.Data.Indexing.Recipes;

namespace Grove.Data.Indexing.Deployments;

public sealed class IndexProfileDeploymentStep : DeploymentStep
{
    public IndexProfileDeploymentStep()
    {
        Name = CreateOrUpdateIndexProfileStep.StepKey;
    }

    public IndexProfileDeploymentStep(IStringLocalizer<IndexProfileDeploymentStep> S)
        : this()
    {
        Category = S["Indexing"];
    }

    public bool IncludeAll { get; set; }

    public string[] IndexNames { get; set; }
}
