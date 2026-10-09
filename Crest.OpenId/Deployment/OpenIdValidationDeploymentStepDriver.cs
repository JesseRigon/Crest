using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.OpenId.Deployment;

public sealed class OpenIdValidationDeploymentStepDriver : DisplayDriver<DeploymentStep, OpenIdValidationDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(OpenIdValidationDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("OpenIdValidationDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("OpenIdValidationDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(OpenIdValidationDeploymentStep step, BuildEditorContext context)
    {
        return View("OpenIdValidationDeploymentStep_Edit", step).Location("Content");
    }
}
