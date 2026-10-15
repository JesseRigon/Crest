using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Facebook.Deployment;

public sealed class FacebookLoginDeploymentStepDriver : DisplayDriver<DeploymentStep, FacebookLoginDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(FacebookLoginDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("FacebookLoginDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("FacebookLoginDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(FacebookLoginDeploymentStep step, BuildEditorContext context)
    {
        return View("FacebookLoginDeploymentStep_Edit", step).Location("Content");
    }
}
