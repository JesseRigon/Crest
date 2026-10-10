using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Indexing;
using Crest.Lucene.ViewModels;

namespace Crest.Lucene.Deployment;

public sealed class LuceneIndexRebuildDeploymentStepDriver : DisplayDriver<DeploymentStep, LuceneIndexRebuildDeploymentStep>
{
    private readonly IIndexProfileStore _indexStore;

    public LuceneIndexRebuildDeploymentStepDriver(IIndexProfileStore indexStore)
    {
        _indexStore = indexStore;
    }

    public override Task<IDisplayResult> DisplayAsync(LuceneIndexRebuildDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("LuceneIndexRebuildDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("LuceneIndexRebuildDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(LuceneIndexRebuildDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<LuceneIndexRebuildDeploymentStepViewModel>("LuceneIndexRebuildDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.IndexNames = step.IndexNames;
            model.AllIndexNames = (await _indexStore.GetByProviderAsync(LuceneConstants.ProviderName)).Select(x => x.IndexName).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(LuceneIndexRebuildDeploymentStep rebuildIndexStep, UpdateEditorContext context)
    {
        rebuildIndexStep.IndexNames = [];

        await context.Updater.TryUpdateModelAsync(rebuildIndexStep, Prefix, step => step.IndexNames, step => step.IncludeAll);

        if (rebuildIndexStep.IncludeAll)
        {
            rebuildIndexStep.IndexNames = [];
        }

        return Edit(rebuildIndexStep, context);
    }
}
