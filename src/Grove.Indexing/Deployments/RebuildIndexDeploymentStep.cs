using Microsoft.Extensions.Localization;
using Grove.Deployment;
using Grove.Indexing.Recipes;

namespace Grove.Indexing.Deployments;

public sealed class RebuildIndexDeploymentStep : DeploymentStep
{
    public RebuildIndexDeploymentStep()
    {
        Name = RebuildIndexStep.Key;
    }

    public RebuildIndexDeploymentStep(IStringLocalizer<RebuildIndexDeploymentStep> S)
        : this()
    {
        Category = S["Indexing"];
    }

    public bool IncludeAll { get; set; }

    public string[] IndexNames { get; set; }
}
