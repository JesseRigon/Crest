using Microsoft.Extensions.Localization;
using Crest.Deployment;
using Crest.Indexing.Recipes;

namespace Crest.Indexing.Deployments;

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
