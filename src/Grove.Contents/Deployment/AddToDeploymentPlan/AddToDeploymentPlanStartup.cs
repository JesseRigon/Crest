using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
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
