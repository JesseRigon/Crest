using Grove.Contents.ViewModels;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Settings;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

public sealed class ExportContentToDeploymentTargetContentsAdminListDisplayDriver : DisplayDriver<ContentOptionsViewModel>
{
    private readonly IDeploymentPlanService _deploymentPlanService;
    private readonly ISiteService _siteService;

    public ExportContentToDeploymentTargetContentsAdminListDisplayDriver(
        IDeploymentPlanService deploymentPlanService,
        ISiteService siteService)
    {
        _deploymentPlanService = deploymentPlanService;
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> DisplayAsync(ContentOptionsViewModel model, BuildDisplayContext context)
    {
        if (await _deploymentPlanService.DoesUserHaveExportPermissionAsync())
        {
            var exportContentToDeploymentTargetSettings = await _siteService.GetSettingsAsync<ExportContentToDeploymentTargetSettings>();

            if (exportContentToDeploymentTargetSettings.ExportContentToDeploymentTargetPlanId != 0)
            {
                return Combine(
                    Dynamic("ExportContentToDeploymentTarget__Button__ContentsBulkActions").Location("BulkActions", "Content:30"),
                    Dynamic("ExportContentToDeploymentTarget_Modal__ContentsBulkActionsDeploymentTarget").Location("BulkActions", "Content:30")
                );
            }
        }

        return null;
    }
}
