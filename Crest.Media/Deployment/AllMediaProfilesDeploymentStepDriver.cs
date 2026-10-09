using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Media.Deployment;

public sealed class AllMediaProfilesDeploymentStepDriver : DisplayDriver<DeploymentStep, AllMediaProfilesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllMediaProfilesDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllMediaProfilesDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllMediaProfilesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllMediaProfilesDeploymentStep step, BuildEditorContext context)
    {
        return View("AllMediaProfilesDeploymentStep_Edit", step).Location("Content");
    }
}
