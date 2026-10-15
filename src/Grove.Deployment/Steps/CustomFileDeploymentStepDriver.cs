using Grove.Deployment.ViewModels;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Deployment.Steps;

public sealed class CustomFileDeploymentStepDriver : DisplayDriver<DeploymentStep, CustomFileDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(CustomFileDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("CustomFileDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("CustomFileDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(CustomFileDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<CustomFileDeploymentStepViewModel>("CustomFileDeploymentStep_Fields_Edit", model =>
        {
            model.FileContent = step.FileContent;
            model.FileName = step.FileName;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(CustomFileDeploymentStep step, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(step, Prefix, x => x.FileName, x => x.FileContent);

        return Edit(step, context);
    }
}
