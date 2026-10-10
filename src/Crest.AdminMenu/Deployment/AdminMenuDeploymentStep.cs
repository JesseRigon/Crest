using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.AdminMenu.Deployment;

/// <summary>
/// Adds all admin menus to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AdminMenuDeploymentStep : DeploymentStep
{
    public AdminMenuDeploymentStep()
    {
        Name = "AdminMenu";
    }

    public AdminMenuDeploymentStep(IStringLocalizer<AdminMenuDeploymentStep> S)
        : this()
    {
        Category = S["Content Management"];
    }
}
