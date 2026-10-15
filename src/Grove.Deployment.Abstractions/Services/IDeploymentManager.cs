using Microsoft.Extensions.FileProviders;

namespace Grove.Deployment.Services;

public interface IDeploymentManager
{
    Task ExecuteDeploymentPlanAsync(DeploymentPlan deploymentPlan, DeploymentPlanResult result);
    Task ImportDeploymentPackageAsync(IFileProvider deploymentPackage);
}
