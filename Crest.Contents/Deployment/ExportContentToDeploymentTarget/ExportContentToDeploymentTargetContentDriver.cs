using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.ViewModels;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;

namespace Crest.Contents.Deployment.ExportContentToDeploymentTarget;

public sealed class ExportContentToDeploymentTargetContentDriver : ContentDisplayDriver
{
    private readonly IDeploymentPlanService _deploymentPlanService;
    private readonly ISiteService _siteService;

    public ExportContentToDeploymentTargetContentDriver(
        IDeploymentPlanService deploymentPlanService,
        ISiteService siteService)
    {
        _deploymentPlanService = deploymentPlanService;
        _siteService = siteService;
    }

    public override Task<IDisplayResult> DisplayAsync(ContentItem model, BuildDisplayContext context)
    {
        return CombineAsync(
            Dynamic("ExportContentToDeploymentTarget_Modal__ActionDeploymentTarget")
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:30")
                .RenderWhen(static async (driver) =>
                {
                    if (await driver._deploymentPlanService.DoesUserHaveExportPermissionAsync())
                    {
                        var exportContentToDeploymentTargetSettings = await driver._siteService.GetSettingsAsync<ExportContentToDeploymentTargetSettings>();

                        if (exportContentToDeploymentTargetSettings.ExportContentToDeploymentTargetPlanId != 0)
                        {
                            return true;
                        }
                    }

                    return false;
                }, this),
            Factory("ExportContentToDeploymentTarget_SummaryAdmin__Button__Actions", static (ContentItem m) => new ContentItemViewModel(m), model)
                .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:40")
                .RenderWhen(static async (driver) =>
                {
                    if (await driver._deploymentPlanService.DoesUserHaveExportPermissionAsync())
                    {
                        var exportContentToDeploymentTargetSettings = await driver._siteService.GetSettingsAsync<ExportContentToDeploymentTargetSettings>();

                        if (exportContentToDeploymentTargetSettings.ExportContentToDeploymentTargetPlanId != 0)
                        {
                            return true;
                        }
                    }

                    return false;
                }, this)
            );
    }
}
