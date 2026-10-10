using Microsoft.Extensions.Localization;
using Crest.Deployment;
using Crest.Indexing.Core.Recipes;

namespace Crest.Indexing.Core.Deployments;

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
