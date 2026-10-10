using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Placements.Deployment;

/// <summary>
/// Adds placements to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class PlacementsDeploymentStep : DeploymentStep
{
    public PlacementsDeploymentStep()
    {
        Name = "Placements";
    }

    public PlacementsDeploymentStep(IStringLocalizer<PlacementsDeploymentStep> S)
        : this()
    {
        Category = S["Development"];
    }
}
