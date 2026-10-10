using Microsoft.Extensions.FileProviders;

namespace Crest.Deployment.Services;

public interface IDeploymentManager
{
    Task ExecuteDeploymentPlanAsync(DeploymentPlan deploymentPlan, DeploymentPlanResult result);
    Task<IEnumerable<DeploymentTarget>> GetDeploymentTargetsAsync();
    Task ImportDeploymentPackageAsync(IFileProvider deploymentPackage);
}
