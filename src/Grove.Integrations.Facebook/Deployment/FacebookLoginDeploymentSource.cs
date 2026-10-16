using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Integrations.Facebook.Services;

namespace Grove.Integrations.Facebook.Deployment;

public sealed class FacebookLoginDeploymentSource
    : DeploymentSourceBase<FacebookLoginDeploymentStep>
{
    private readonly IFacebookService _facebookService;

    public FacebookLoginDeploymentSource(IFacebookService facebookService)
    {
        _facebookService = facebookService;
    }

    protected override async Task ProcessAsync(FacebookLoginDeploymentStep step, DeploymentPlanResult result)
    {
        var settings = await _facebookService.GetSettingsAsync();

        // The 'name' property should match the related recipe step name.
        var jObject = new JsonObject { ["name"] = "FacebookCoreSettings" };

        // Merge settings as the recipe step doesn't use a child property. The app secret is a
        // sealed credential and is never exported; the app id is, so the target knows which app.
        jObject.Merge(JObject.FromObject(settings));
        jObject["AppId"] = await _facebookService.GetAppIdAsync();

        result.Steps.Add(jObject);
    }
}
