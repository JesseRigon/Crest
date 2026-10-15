using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Microsoft.Authentication.Deployment;

public sealed class MicrosoftAccountDeploymentStepDriver : DisplayDriver<DeploymentStep, MicrosoftAccountDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(MicrosoftAccountDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("MicrosoftAccountDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
            View("MicrosoftAccountDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(MicrosoftAccountDeploymentStep step, BuildEditorContext context)
        => View("MicrosoftAccountDeploymentStep_Edit", step).Location("Content");
}
