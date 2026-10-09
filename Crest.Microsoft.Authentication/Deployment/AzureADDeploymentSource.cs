using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Microsoft.Authentication.Services;
using Crest.Microsoft.Authentication.Settings;

namespace Crest.Microsoft.Authentication.Deployment;

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
