using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Crest.Indexing;
using Crest.Indexing.Core;
using Crest.Indexing.Models;
using Crest.Recipes.Models;
using Crest.Recipes.Services;

namespace Crest.AzureAI.Recipes;

public sealed class AzureAISearchIndexSettingsStep : NamedRecipeStepHandler
{
    public const string Name = "azureai-index-create";

    private readonly IIndexProfileManager _indexProfileManager;

    internal readonly IStringLocalizer S;

    public AzureAISearchIndexSettingsStep(
        IIndexProfileManager indexProfileManager,
        IStringLocalizer<AzureAISearchIndexSettingsStep> stringLocalizer)
        : base(Name)
    {
        _indexProfileManager = indexProfileManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Indices"] is not JsonArray tokens)
        {
            return;
        }

        foreach (var token in tokens)
        {
            IndexProfile index = null;

            var id = token[nameof(index.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                index = await _indexProfileManager.FindByIdAsync(id);
            }

            if (index is not null)
            {
                await _indexProfileManager.UpdateAsync(index, token);
            }
            else
            {
                var type = token[nameof(index.Type)]?.GetValue<string>() ?? IndexingConstants.ContentsIndexSource;

                index = await _indexProfileManager.NewAsync(AzureAISearchConstants.ProviderName, type, token);
            }

            var validationResult = await _indexProfileManager.ValidateAsync(index);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _indexProfileManager.CreateAsync(index);
        }
    }
}
