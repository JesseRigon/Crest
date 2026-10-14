#nullable enable

using Crest.Deployment.Api;

namespace Crest.Deployment;

/// <summary>
/// The instances a plan can be sent to, listed on the plan's page. Registered by the feature
/// that sends plans (remote deployment); without one a plan lists none.
/// </summary>
public interface IRemoteDeploymentTargets
{
    Task<IReadOnlyList<DeploymentTargetRef>> ListAsync();
}
