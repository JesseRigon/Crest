using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Modules;

namespace Grove.Contents.Deployment.AddToDeploymentPlan;

[Feature("Grove.Contents.Deployment.AddToDeploymentPlan")]
public sealed class AddToDeploymentPlanStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContentItemDeploymentSource, ContentItemDeploymentStep>();
    }
}
