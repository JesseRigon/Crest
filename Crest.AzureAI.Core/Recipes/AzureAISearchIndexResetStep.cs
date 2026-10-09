using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Crest.BackgroundJobs;
using Crest.Indexing;
using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.AzureAI.Deployment;

namespace Crest.AzureAI.Recipes;

public sealed class AzureAISearchIndexResetStep : NamedRecipeStepHandler
{
    public AzureAISearchIndexResetStep()
        : base(AzureAISearchIndexResetDeploymentSource.Name)
    {
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AzureAISearchIndexResetDeploymentStep>();

        if (model == null)
        {
            return;
        }

        if (!model.IncludeAll && (model.Indices == null || model.Indices.Length == 0))
        {
            return;
        }

        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(AzureAISearchIndexRebuildDeploymentSource.Name, async scope =>
        {
            var indexProfileManager = scope.ServiceProvider.GetRequiredService<IIndexProfileManager>();

            var indexes = model.IncludeAll
            ? await indexProfileManager.GetByProviderAsync(AzureAISearchConstants.ProviderName)
            : (await indexProfileManager.GetByProviderAsync(AzureAISearchConstants.ProviderName))
                .Where(x => model.Indices.Contains(x.IndexName, StringComparer.OrdinalIgnoreCase));

            foreach (var index in indexes)
            {
                await indexProfileManager.ResetAsync(index);
                await indexProfileManager.UpdateAsync(index);
                await indexProfileManager.SynchronizeAsync(index);
            }
        });
    }
}
