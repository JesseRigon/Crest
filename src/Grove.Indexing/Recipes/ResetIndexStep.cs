using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundJobs;
using Grove.Indexing.Deployments;
using Grove.Recipes.Models;
using Grove.Recipes.Services;

namespace Grove.Indexing.Recipes;

public sealed class ResetIndexStep : NamedRecipeStepHandler
{
    public const string Key = "ResetIndex";

    public ResetIndexStep()
        : base(Key)
    {
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<IndexProfileDeploymentStep>();

        if (model == null)
        {
            return;
        }

        if (!model.IncludeAll && (model.IndexNames == null || model.IndexNames.Length == 0))
        {
            return;
        }

        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("reset-indexes", async scope =>
        {
            var indexProfileManager = scope.ServiceProvider.GetService<IIndexProfileManager>();

            var indexes = model.IncludeAll
            ? await indexProfileManager.GetAllAsync()
            : (await indexProfileManager.GetAllAsync()).Where(x => model.IndexNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase));

            foreach (var index in indexes)
            {
                await indexProfileManager.ResetAsync(index);
                await indexProfileManager.UpdateAsync(index);
                await indexProfileManager.SynchronizeAsync(index);
            }
        });
    }
}
