using Crest.Contents.ViewModels;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Contents.Deployment;

public sealed class AllContentDeploymentStepDriver : DisplayDriver<DeploymentStep, AllContentDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllContentDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllContentDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllContentDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllContentDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AllContentDeploymentStepViewModel>("AllContentDeploymentStep_Fields_Edit", model =>
        {
            model.ExportAsSetupRecipe = step.ExportAsSetupRecipe;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AllContentDeploymentStep step, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(step, Prefix, x => x.ExportAsSetupRecipe);

        return Edit(step, context);
    }
}
