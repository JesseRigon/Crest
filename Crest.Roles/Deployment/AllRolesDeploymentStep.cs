using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Roles.Deployment;

/// <summary>
/// Adds roles to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AllRolesDeploymentStep : DeploymentStep
{
    public AllRolesDeploymentStep()
    {
        Name = "AllRoles";
    }

    public AllRolesDeploymentStep(IStringLocalizer<AllRolesDeploymentStep> S)
        : this()
    {
        Category = S["Security"];
    }
}
