using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Themes.Deployment;

public sealed class ThemesDeploymentStepDriver : DisplayDriver<DeploymentStep, ThemesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(ThemesDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
                View("ThemesDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("ThemesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(ThemesDeploymentStep step, BuildEditorContext context)
    {
        return View("ThemesDeploymentStep_Edit", step).Location("Content");
    }
}
