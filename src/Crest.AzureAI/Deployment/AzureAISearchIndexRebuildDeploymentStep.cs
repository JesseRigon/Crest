using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.AzureAI.Deployment;

public class AzureAISearchIndexRebuildDeploymentStep : DeploymentStep
{
    public AzureAISearchIndexRebuildDeploymentStep()
    {
        Name = "AzureAISearchIndexRebuild";
    }

    public AzureAISearchIndexRebuildDeploymentStep(IStringLocalizer<AzureAISearchIndexRebuildDeploymentStep> S)
        : this()
    {
        Category = S["Search"];
    }

    public bool IncludeAll { get; set; }

    public string[] Indices { get; set; }
}
