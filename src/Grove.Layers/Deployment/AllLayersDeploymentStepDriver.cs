using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Layers.Deployment;

public sealed class AllLayersDeploymentStepDriver : DisplayDriver<DeploymentStep, AllLayersDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllLayersDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllLayersDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllLayersDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllLayersDeploymentStep step, BuildEditorContext context)
    {
        return View("AllLayersDeploymentStep_Edit", step).Location("Content");
    }
}
