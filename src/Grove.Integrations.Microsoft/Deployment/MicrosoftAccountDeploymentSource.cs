using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;

namespace Grove.Integrations.Microsoft.Deployment;

public sealed class MicrosoftAccountDeploymentSource
    : DeploymentSourceBase<MicrosoftAccountDeploymentStep>
{
    private readonly IMicrosoftAccountService _microsoftAccountService;

    public MicrosoftAccountDeploymentSource(IMicrosoftAccountService microsoftAccountService)
    {
        _microsoftAccountService = microsoftAccountService;
    }

    protected override async Task ProcessAsync(MicrosoftAccountDeploymentStep step, DeploymentPlanResult result)
    {
        var microsoftAccountSettings = await _microsoftAccountService.GetSettingsAsync();

        result.Steps.Add(new JsonObject
        {
            ["name"] = nameof(MicrosoftAccountSettings),
            ["MicrosoftAccountSettings"] = JObject.FromObject(microsoftAccountSettings),
        });
    }
}
