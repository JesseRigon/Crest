using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.OpenId.Deployment;

/// <summary>
/// Adds Open ID settings to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class OpenIdValidationDeploymentStep : DeploymentStep
{
    public OpenIdValidationDeploymentStep()
    {
        Name = "OpenID Validation";
    }

    public OpenIdValidationDeploymentStep(IStringLocalizer<OpenIdValidationDeploymentStep> S)
        : this()
    {
        Category = S["OpenID Connect"];
    }
}
