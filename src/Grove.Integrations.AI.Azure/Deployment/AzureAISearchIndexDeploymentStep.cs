using Microsoft.Extensions.Localization;
using Grove.Integrations.AI.Azure.Recipes;
using Grove.Deployment;

namespace Grove.Integrations.AI.Azure.Deployment;

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
