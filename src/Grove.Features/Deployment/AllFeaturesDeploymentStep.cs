using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Features.Deployment;

/// <summary>
/// Adds enabled and disabled features to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AllFeaturesDeploymentStep : DeploymentStep
{
    public AllFeaturesDeploymentStep()
    {
        Name = "AllFeatures";
    }

    public AllFeaturesDeploymentStep(IStringLocalizer<AllFeaturesDeploymentStep> S)
        : this()
    {
        Category = S["Infrastructure"];
    }

    public bool IgnoreDisabledFeatures { get; set; }
}
