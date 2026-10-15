using Microsoft.Extensions.Localization;
using Grove.AzureAI.Recipes;
using Grove.Deployment;

namespace Grove.AzureAI.Deployment;

public class AzureAISearchIndexDeploymentStep : DeploymentStep
{
    public AzureAISearchIndexDeploymentStep()
    {
        Name = AzureAISearchIndexSettingsStep.Name;
    }

    public AzureAISearchIndexDeploymentStep(IStringLocalizer<AzureAISearchIndexDeploymentStep> S)
        : this()
    {
        Category = S["Search"];
    }

    public bool IncludeAll { get; set; } = true;

    public string[] IndexNames { get; set; }
}
