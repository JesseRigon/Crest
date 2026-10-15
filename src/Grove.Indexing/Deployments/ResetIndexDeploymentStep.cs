using Microsoft.Extensions.Localization;
using Grove.Deployment;
using Grove.Indexing.Recipes;

namespace Grove.Indexing.Deployments;

public sealed class ResetIndexDeploymentStep : DeploymentStep
{
    public ResetIndexDeploymentStep()
    {
        Name = ResetIndexStep.Key;
    }

    public ResetIndexDeploymentStep(IStringLocalizer<ResetIndexDeploymentStep> S)
        : this()
    {
        Category = S["Indexing"];
    }

    public bool IncludeAll { get; set; }

    public string[] IndexNames { get; set; }
}
