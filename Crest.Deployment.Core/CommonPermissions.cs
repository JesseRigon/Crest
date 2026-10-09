using Crest.Security.Permissions;

namespace Crest.Deployment;

/// <summary>
/// This class contains the source references to the Crest.Deployment module permissions so they can be used in other modules
/// without having to reference the Crest.Deployment by itself.
/// </summary>
[Obsolete("This will be removed in a future release. Instead use 'Crest.Deployment.DeploymentPermissions'.")]
public static class CommonPermissions
{
    public static readonly Permission ManageDeploymentPlan = DeploymentPermissions.ManageDeploymentPlan;

    public static readonly Permission Export = DeploymentPermissions.Export;

    public static readonly Permission Import = DeploymentPermissions.Import;
}
