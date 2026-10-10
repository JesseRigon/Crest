using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.AdminMenu.Deployment;

public sealed class AdminMenuDeploymentStepDriver : DisplayDriver<DeploymentStep, AdminMenuDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AdminMenuDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AdminMenuDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AdminMenuDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AdminMenuDeploymentStep step, BuildEditorContext context)
    {
        return View("AdminMenuDeploymentStep_Fields_Edit", step).Location("Content");
    }
}
