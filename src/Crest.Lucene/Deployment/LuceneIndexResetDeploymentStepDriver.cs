using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Indexing;
using Crest.Lucene.ViewModels;

namespace Crest.Lucene.Deployment;

public sealed class LuceneIndexResetDeploymentStepDriver : DisplayDriver<DeploymentStep, LuceneIndexResetDeploymentStep>
{
    private readonly IIndexProfileStore _indexStore;

    public LuceneIndexResetDeploymentStepDriver(IIndexProfileStore indexStore)
    {
        _indexStore = indexStore;
    }

    public override Task<IDisplayResult> DisplayAsync(LuceneIndexResetDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("LuceneIndexResetDeploymentStep_Fields_Summary", step)
                    .Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("LuceneIndexResetDeploymentStep_Fields_Thumbnail", step)
                    .Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(LuceneIndexResetDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<LuceneIndexResetDeploymentStepViewModel>("LuceneIndexResetDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.IndexNames = step.IndexNames;
            model.AllIndexNames = (await _indexStore.GetByProviderAsync(LuceneConstants.ProviderName)).Select(x => x.IndexName).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(LuceneIndexResetDeploymentStep resetIndexStep, UpdateEditorContext context)
    {
        resetIndexStep.IndexNames = [];

        await context.Updater.TryUpdateModelAsync(resetIndexStep, Prefix, step => step.IndexNames, step => step.IncludeAll);

        if (resetIndexStep.IncludeAll)
        {
            resetIndexStep.IndexNames = [];
        }

        return Edit(resetIndexStep, context);
    }
}
