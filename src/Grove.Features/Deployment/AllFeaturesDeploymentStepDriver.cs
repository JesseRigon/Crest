using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Features.ViewModels;

namespace Grove.Features.Deployment;

public sealed class AllFeaturesDeploymentStepDriver : DisplayDriver<DeploymentStep, AllFeaturesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllFeaturesDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllFeaturesDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllFeaturesDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllFeaturesDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AllFeaturesDeploymentStepViewModel>("AllFeaturesDeploymentStep_Fields_Edit", model =>
        {
            model.IgnoreDisabledFeatures = step.IgnoreDisabledFeatures;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AllFeaturesDeploymentStep step, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(step, Prefix, x => x.IgnoreDisabledFeatures);

        return Edit(step, context);
    }
}
