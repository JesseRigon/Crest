using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Sitemaps.Deployment;

public sealed class AllSitemapsDeploymentStep : DeploymentStep
{
    public AllSitemapsDeploymentStep()
    {
        Name = "AllSitemaps";
    }

    public AllSitemapsDeploymentStep(IStringLocalizer<AllSitemapsDeploymentStep> S)
        : this()
    {
        Category = S["Content Management"];
    }
}
