using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Layers.Deployment;

/// <summary>
/// Adds layers to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AllLayersDeploymentStep : DeploymentStep
{
    public AllLayersDeploymentStep()
    {
        Name = "AllLayers";
    }

    public AllLayersDeploymentStep(IStringLocalizer<AllLayersDeploymentStep> S)
        : this()
    {
        Category = S["Content"];
    }
}
