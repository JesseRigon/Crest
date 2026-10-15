using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Access.Users.Deployment;

/// <summary>
/// Adds users to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AllUsersDeploymentStep : DeploymentStep
{
    public AllUsersDeploymentStep()
    {
        Name = "AllUsers";
    }

    public AllUsersDeploymentStep(IStringLocalizer<AllUsersDeploymentStep> S)
        : this()
    {
        Category = S["Security"];
    }
}
