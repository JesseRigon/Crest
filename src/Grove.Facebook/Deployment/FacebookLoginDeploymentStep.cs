using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Facebook.Deployment;

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
