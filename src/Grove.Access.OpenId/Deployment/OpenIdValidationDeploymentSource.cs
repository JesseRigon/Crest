using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Access.OpenId.Services;
using Grove.Access.OpenId.Settings;

namespace Grove.Access.OpenId.Deployment;

public sealed class OpenIdValidationDeploymentSource
    : DeploymentSourceBase<OpenIdValidationDeploymentStep>
{
    private readonly IOpenIdValidationService _openIdValidationService;

    public OpenIdValidationDeploymentSource(IOpenIdValidationService openIdValidationService)
    {
        _openIdValidationService = openIdValidationService;
    }

    protected override async Task ProcessAsync(OpenIdValidationDeploymentStep step, DeploymentPlanResult result)
    {
        var validationSettings = await _openIdValidationService.GetSettingsAsync();

        result.Steps.Add(new JsonObject
        {
            ["name"] = nameof(OpenIdValidationSettings),
            ["OpenIdValidationSettings"] = JObject.FromObject(validationSettings),
        });
    }
}
