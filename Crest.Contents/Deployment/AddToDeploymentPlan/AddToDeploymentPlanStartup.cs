using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Contents.ViewModels;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;

namespace Crest.Contents.Deployment.AddToDeploymentPlan;

[Feature("Crest.Contents.Deployment.AddToDeploymentPlan")]
public sealed class AddToDeploymentPlanStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ContentItemDeploymentSource, ContentItemDeploymentStep, ContentItemDeploymentStepDriver>();
        services.AddScoped<IContentDisplayDriver, AddToDeploymentPlanContentDriver>();
        services.AddDisplayDriver<ContentOptionsViewModel, AddToDeploymentPlanContentsAdminListDisplayDriver>();
    }
}
