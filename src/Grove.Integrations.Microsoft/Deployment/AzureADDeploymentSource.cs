using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Deployment;

public sealed class AzureADDeploymentSource
    : DeploymentSourceBase<AzureADDeploymentStep>
{
    private readonly IAzureADService _azureADService;

    public AzureADDeploymentSource(IAzureADService azureADService)
    {
        _azureADService = azureADService;
    }

    protected override async Task ProcessAsync(AzureADDeploymentStep step, DeploymentPlanResult result)
    {
        var azureADSettings = await _azureADService.GetSettingsAsync();

        result.Steps.Add(new JsonObject
        {
            ["name"] = nameof(AzureADSettings),
            ["AzureADSettings"] = JObject.FromObject(azureADSettings),
        });
    }
}
