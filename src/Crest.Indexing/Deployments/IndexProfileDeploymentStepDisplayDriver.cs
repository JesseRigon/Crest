using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Indexing.Deployments;
using Crest.Indexing.Deployments.ViewModels;
using Crest.Mvc.ModelBinding;

namespace Crest.Indexing.Deployments;

internal sealed class IndexProfileDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, IndexProfileDeploymentStep>
{
    private readonly IIndexProfileStore _store;

    internal readonly IStringLocalizer S;

    public IndexProfileDeploymentStepDisplayDriver(
        IIndexProfileStore store,
        IStringLocalizer<IndexProfileDeploymentStepDisplayDriver> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(IndexProfileDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("IndexProfileDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("IndexProfileDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(IndexProfileDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<IndexProfileDeploymentStepViewModel>("IndexProfileDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Indexes = (await _store.GetAllAsync()).Select(x => new SelectListItem(x.Name, x.Name)
            {
                Selected = step.IndexNames?.Contains(x.Name) ?? false,
            }).OrderBy(x => x.Text)
            .ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(IndexProfileDeploymentStep step, UpdateEditorContext context)
    {
        var model = new IndexProfileDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.IndexNames = [];
        }
        else
        {
            var selectedIndexNames = model.Indexes?.Where(x => x.Selected).Select(x => x.Value).ToArray() ?? [];

            if (selectedIndexNames.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Indexes), S["At least one index profile is required."]);
            }

            step.IncludeAll = false;
            step.IndexNames = selectedIndexNames;
        }

        return Edit(step, context);
    }
}
