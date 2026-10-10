using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.ViewModels;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Contents.Deployment.AddToDeploymentPlan;

public sealed class AddToDeploymentPlanContentDriver : ContentDisplayDriver
{
    private readonly IDeploymentPlanService _deploymentPlanService;

    public AddToDeploymentPlanContentDriver(IDeploymentPlanService deploymentPlanService)
    {
        _deploymentPlanService = deploymentPlanService;
    }

    public override Task<IDisplayResult> DisplayAsync(ContentItem model, BuildDisplayContext context)
    {
        return CombineAsync(
                Dynamic("AddToDeploymentPlan_Modal__ActionDeploymentPlan")
                    .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:30")
                    .RenderWhen(static (deploymentPlanService) => deploymentPlanService.DoesUserHavePermissionsAsync(), _deploymentPlanService),
                Factory("AddToDeploymentPlan_SummaryAdmin__Button__Actions", static (ContentItem m) => new ContentItemViewModel(m), model)
                    .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:30")
                    .RenderWhen(static (deploymentPlanService) => deploymentPlanService.DoesUserHavePermissionsAsync(), _deploymentPlanService)
            );
    }
}
