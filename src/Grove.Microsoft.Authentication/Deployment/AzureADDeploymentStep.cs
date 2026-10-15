using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Microsoft.Authentication.Deployment;

/// <summary>
/// Adds Microsoft Entra ID settings to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AzureADDeploymentStep : DeploymentStep
{
    public AzureADDeploymentStep()
    {
        Name = "Microsoft Entra ID";
    }

    public AzureADDeploymentStep(IStringLocalizer<AzureADDeploymentStep> S)
        : this()
    {
        Category = S["Microsoft Authentication"];
    }
}
