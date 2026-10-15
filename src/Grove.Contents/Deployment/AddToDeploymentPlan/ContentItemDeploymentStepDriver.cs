using Microsoft.Extensions.Localization;
using Grove.ContentManagement;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;

namespace Grove.Contents.Deployment.AddToDeploymentPlan;

public sealed class ContentItemDeploymentStepDriver : DisplayDriver<DeploymentStep, ContentItemDeploymentStep>
{
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    public ContentItemDeploymentStepDriver(IContentManager contentManager,
        IStringLocalizer<ContentItemDeploymentStepDriver> stringLocalizer)
    {
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ContentItemDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("ContentItemDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("ContentItemDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(ContentItemDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<ContentItemDeploymentStepViewModel>("ContentItemDeploymentStep_Fields_Edit", model =>
        {
            model.ContentItemId = step.ContentItemId;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItemDeploymentStep step, UpdateEditorContext context)
    {
        var model = new ContentItemDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, x => x.ContentItemId);
        var contentItem = await _contentManager.GetAsync(model.ContentItemId);

        if (contentItem == null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(step.ContentItemId), S["Your content item does not exist."]);
        }
        else
        {
            step.ContentItemId = model.ContentItemId;
        }

        return Edit(step, context);
    }
}
