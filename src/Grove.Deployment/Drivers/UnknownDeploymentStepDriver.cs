using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Deployment.Drivers;

public sealed class UnknownDeploymentStepDriver : DisplayDriver<DeploymentStep, UnknownDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(UnknownDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("UnknownDeploymentStep_Fields_Summary", step)
                .Location(PlatformConstants.DisplayType.Summary, "Content"),
            View("UnknownDeploymentStep_Fields_Thumbnail", step)
                .Location("Thumbnail", "Content")
        );
    }
}
