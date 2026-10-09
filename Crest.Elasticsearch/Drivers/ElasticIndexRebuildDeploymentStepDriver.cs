using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Indexing;
using Crest.Elasticsearch.ViewModels;

namespace Crest.Elasticsearch.Core.Deployment;

public sealed class ElasticIndexRebuildDeploymentStepDriver : DisplayDriver<DeploymentStep, ElasticsearchIndexRebuildDeploymentStep>
{
    private readonly IIndexProfileStore _indexStore;

    public ElasticIndexRebuildDeploymentStepDriver(IIndexProfileStore indexStore)
    {
        _indexStore = indexStore;
    }

    public override Task<IDisplayResult> DisplayAsync(ElasticsearchIndexRebuildDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("ElasticIndexRebuildDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("ElasticIndexRebuildDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(ElasticsearchIndexRebuildDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<ElasticIndexRebuildDeploymentStepViewModel>("ElasticIndexRebuildDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.IndexNames = step.Indices;
            model.AllIndexNames = (await _indexStore.GetByProviderAsync(ElasticsearchConstants.ProviderName)).Select(x => x.IndexName).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ElasticsearchIndexRebuildDeploymentStep rebuildIndexStep, UpdateEditorContext context)
    {
        rebuildIndexStep.Indices = [];

        await context.Updater.TryUpdateModelAsync(rebuildIndexStep, Prefix, step => step.Indices, step => step.IncludeAll);

        if (rebuildIndexStep.IncludeAll)
        {
            rebuildIndexStep.Indices = [];
        }

        return Edit(rebuildIndexStep, context);
    }
}
