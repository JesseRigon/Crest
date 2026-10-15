using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Tenants.Deployment;

public class AllFeatureProfilesDeploymentStep : DeploymentStep
{
    public AllFeatureProfilesDeploymentStep()
    {
        Name = "AllFeatureProfiles";
    }

    public AllFeatureProfilesDeploymentStep(IStringLocalizer<AllFeatureProfilesDeploymentStep> S)
        : this()
    {
        Category = S["Infrastructure"];
    }
}
