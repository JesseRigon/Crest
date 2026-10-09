using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.OpenId.Services;
using Crest.OpenId.Settings;

namespace Crest.OpenId.Deployment;

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
