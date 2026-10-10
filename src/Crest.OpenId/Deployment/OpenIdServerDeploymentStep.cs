using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.OpenId.Deployment;

/// <summary>
/// Adds Open ID settings to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class OpenIdServerDeploymentStep : DeploymentStep
{
    public OpenIdServerDeploymentStep()
    {
        Name = "OpenID Server";
    }

    public OpenIdServerDeploymentStep(IStringLocalizer<OpenIdServerDeploymentStep> S)
        : this()
    {
        Category = S["OpenID Connect"];
    }
}
