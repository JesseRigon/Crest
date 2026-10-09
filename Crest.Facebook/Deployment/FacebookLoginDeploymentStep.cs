using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Facebook.Deployment;

/// <summary>
/// Adds Facebook Login settings to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class FacebookLoginDeploymentStep : DeploymentStep
{
    public FacebookLoginDeploymentStep()
    {
        Name = "Facebook Login";
    }

    public FacebookLoginDeploymentStep(IStringLocalizer<FacebookLoginDeploymentStep> S)
        : this()
    {
        Category = S["Meta"];
    }
}
