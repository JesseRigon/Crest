using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Sitemaps.Deployment;

public sealed class AllSitemapsDeploymentStepDriver : DisplayDriver<DeploymentStep, AllSitemapsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllSitemapsDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllSitemapsDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllSitemapsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllSitemapsDeploymentStep step, BuildEditorContext context)
    {
        return View("AllSitemapsDeploymentStep_Edit", step).Location("Content");
    }
}
