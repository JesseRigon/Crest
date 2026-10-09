using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Deployment.Drivers;

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
