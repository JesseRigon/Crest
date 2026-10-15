using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.AzureAI.Deployment;

public class AzureAISearchIndexResetDeploymentStep : DeploymentStep
{
    public AzureAISearchIndexResetDeploymentStep()
    {
        Name = "AzureAISearchIndexReset";
    }

    public AzureAISearchIndexResetDeploymentStep(IStringLocalizer<AzureAISearchIndexResetDeploymentStep> S)
        : this()
    {
        Category = S["Search"];
    }

    public bool IncludeAll { get; set; }

    public string[] Indices { get; set; }
}
