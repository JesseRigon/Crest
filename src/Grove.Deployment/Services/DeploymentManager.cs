using Microsoft.Extensions.FileProviders;
using Grove.Deployment.Services;

namespace Grove.Deployment.Services;

public class DeploymentManager : IDeploymentManager
{
    private readonly IEnumerable<IDeploymentSource> _deploymentSources;
    private readonly IEnumerable<IDeploymentTargetHandler> _deploymentTargetHandlers;

    public DeploymentManager(
        IEnumerable<IDeploymentSource> deploymentSources,
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers)
    {
        _deploymentSources = deploymentSources;
        _deploymentTargetHandlers = deploymentTargetHandlers;
    }

    public async Task ExecuteDeploymentPlanAsync(DeploymentPlan deploymentPlan, DeploymentPlanResult result)
    {
        foreach (var step in deploymentPlan.DeploymentSteps)
        {
            foreach (var source in _deploymentSources)
            {
                await source.ProcessDeploymentStepAsync(step, result);
            }
        }

        await result.FinalizeAsync();
    }

    public async Task ImportDeploymentPackageAsync(IFileProvider deploymentPackage)
    {
        foreach (var deploymentTargetHandler in _deploymentTargetHandlers)
        {
            // Don't trigger in parallel to avoid potential race conditions in the handlers
            await deploymentTargetHandler.ImportFromFileAsync(deploymentPackage);
        }
    }
}
