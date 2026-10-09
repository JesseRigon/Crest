using Microsoft.Extensions.Localization;
using Crest.Deployment;
using Crest.Indexing.Core.Recipes;

namespace Crest.Indexing.Core.Deployments;

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
