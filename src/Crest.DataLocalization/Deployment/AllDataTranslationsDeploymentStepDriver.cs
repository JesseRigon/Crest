using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.DataLocalization.Deployment;

public class AllDataTranslationsDeploymentStepDriver : DisplayDriver<DeploymentStep, AllDataTranslationsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllDataTranslationsDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllDataTranslationsDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllDataTranslationsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllDataTranslationsDeploymentStep step, BuildEditorContext context)
    {
        return View("AllDataTranslationsDeploymentStep_Edit", step).Location("Content");
    }
}
