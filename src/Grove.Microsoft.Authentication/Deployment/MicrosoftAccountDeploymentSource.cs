using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Microsoft.Authentication.Services;
using Grove.Microsoft.Authentication.Settings;

namespace Grove.Microsoft.Authentication.Deployment;

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
