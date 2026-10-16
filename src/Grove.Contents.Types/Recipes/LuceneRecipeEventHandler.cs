using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Records;
using Grove.Infrastructure.Recipes.Events;
using Grove.Infrastructure.Recipes.Models;

namespace Grove.Contents.Types;

/// <summary>
/// This handler provides backward compatibility with ContentIndexSettings that have been migrated to LuceneContentIndexSettings.
/// </summary>
public class LuceneRecipeEventHandler : IRecipeEventHandler
{
    public RecipeExecutionContext Context { get; private set; }

    public Task RecipeStepExecutedAsync(RecipeExecutionContext context) => Task.CompletedTask;

    public Task ExecutionFailedAsync(string executionId, RecipeDescriptor descriptor) => Task.CompletedTask;

    public Task RecipeExecutedAsync(string executionId, RecipeDescriptor descriptor) => Task.CompletedTask;

    public Task RecipeExecutingAsync(string executionId, RecipeDescriptor descriptor) => Task.CompletedTask;

    public Task RecipeStepExecutingAsync(RecipeExecutionContext context)
    {
        if (context.Name == "ReplaceContentDefinition" || context.Name == "ContentDefinition")
        {
            var step = context.Step.ToObject<ContentDefinitionStepModel>();

            foreach (var contentType in step.ContentTypes)
            {
                foreach (var partDefinition in contentType.ContentTypeFieldSetDefinitionRecords)
                {
                    if (partDefinition.Settings != null)
                    {
                        if (partDefinition.Settings.TryGetPropertyValue("ContentIndexSettings", out var existingFieldSetSettings) &&
                            !partDefinition.Settings.ContainsKey("LuceneContentIndexSettings"))
                        {
                            partDefinition.Settings.Add("LuceneContentIndexSettings", existingFieldSetSettings?.DeepClone());
                        }

                        partDefinition.Settings.Remove("ContentIndexSettings");
                    }
                }
            }

            foreach (var partDefinition in step.ContentFieldSets)
            {
                if (partDefinition.Settings != null)
                {
                    if (partDefinition.Settings.TryGetPropertyValue("ContentIndexSettings", out var existingFieldSetSettings))
                    {
                        partDefinition.Settings["LuceneContentIndexSettings"] = existingFieldSetSettings?.DeepClone();
                    }

                    partDefinition.Settings.Remove("ContentIndexSettings");

                    foreach (var fieldDefinition in partDefinition.ContentFieldDefinitionRecords)
                    {
                        if (fieldDefinition.Settings != null)
                        {
                            if (fieldDefinition.Settings.TryGetPropertyValue("ContentIndexSettings", out var existingFieldSettings))
                            {
                                fieldDefinition.Settings["LuceneContentIndexSettings"] = existingFieldSettings?.DeepClone();
                            }

                            fieldDefinition.Settings.Remove("ContentIndexSettings");
                        }
                    }
                }
            }

            context.Step = JObject.FromObject(step);
        }

        return Task.CompletedTask;
    }

    private sealed class ContentDefinitionStepModel
    {
        public string Name { get; set; }
        public ContentTypeDefinitionRecord[] ContentTypes { get; set; } = [];
        public ContentFieldSetDefinitionRecord[] ContentFieldSets { get; set; } = [];
    }
}
