using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Indexing;
using Crest.AzureAI.Deployment;
using Crest.AzureAI.ViewModels;

namespace Crest.AzureAI.Drivers;

public sealed class AzureAISearchIndexDeploymentStepDriver : DisplayDriver<DeploymentStep, AzureAISearchIndexDeploymentStep>
{
    private readonly IIndexProfileStore _store;

    public AzureAISearchIndexDeploymentStepDriver(IIndexProfileStore store)
    {
        _store = store;
    }
    public override Task<IDisplayResult> DisplayAsync(AzureAISearchIndexDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AzureAISearchIndexDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
            View("AzureAISearchIndexDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    public override IDisplayResult Edit(AzureAISearchIndexDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AzureAISearchIndexDeploymentStepViewModel>("AzureAISearchIndexDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.IndexNames = step.IndexNames;
            model.AllIndexNames = (await _store.GetByProviderAsync(AzureAISearchConstants.ProviderName)).Select(x => x.IndexName).ToArray();
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AzureAISearchIndexDeploymentStep step, UpdateEditorContext context)
    {
        step.IndexNames = [];

        await context.Updater.TryUpdateModelAsync(step, Prefix,
            p => p.IncludeAll,
            p => p.IndexNames);

        if (step.IncludeAll)
        {
            // Clear index names if the user select include all.
            step.IndexNames = [];
        }

        return Edit(step, context);
    }
}
