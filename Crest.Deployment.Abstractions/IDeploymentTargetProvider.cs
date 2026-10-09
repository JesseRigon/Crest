namespace Crest.Deployment;

public interface IDeploymentTargetProvider
{
    Task<IEnumerable<DeploymentTarget>> GetDeploymentTargetsAsync();
}
