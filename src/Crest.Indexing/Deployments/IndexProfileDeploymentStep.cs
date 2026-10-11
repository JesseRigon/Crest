using Microsoft.Extensions.Localization;
using Crest.Deployment;
using Crest.Indexing.Recipes;

namespace Crest.Indexing.Deployments;

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
