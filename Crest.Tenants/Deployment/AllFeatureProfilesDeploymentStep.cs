using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Tenants.Deployment;

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
