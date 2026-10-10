using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Templates.ViewModels;

namespace Crest.Templates.Deployment;

public sealed class AllAdminTemplatesDeploymentStepDriver : DisplayDriver<DeploymentStep, AllAdminTemplatesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllAdminTemplatesDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllAdminTemplatesDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllAdminTemplatesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllAdminTemplatesDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AllAdminTemplatesDeploymentStepViewModel>("AllAdminTemplatesDeploymentStep_Fields_Edit", model =>
        {
            model.ExportAsFiles = step.ExportAsFiles;
        }).Location("Content");
    }
    public override async Task<IDisplayResult> UpdateAsync(AllAdminTemplatesDeploymentStep step, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(step, Prefix, x => x.ExportAsFiles);

        return Edit(step, context);
    }
}
