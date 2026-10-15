using Grove.Recipes.Events;
using Grove.Recipes.Models;

namespace Grove.OptionPicker.ContentItems;

/// <summary>
/// Old recipes still import: a content definition step's content picker fields become option
/// pickers over the content items source, and a content step's values in the retired shape
/// (<c>ContentItemIds</c>) become <c>SelectedIds</c>, on the way in (<see cref="ContentPickerConversion"/>).
/// </summary>
public sealed class ContentPickerRecipeMapping : IRecipeEventHandler
{
    public Task RecipeStepExecutingAsync(RecipeExecutionContext context)
    {
        switch (context.Name)
        {
            case "ContentDefinition" or "ReplaceContentDefinition":
                ContentPickerConversion.ConvertDefinitionStep(context.Step);
                break;
            case "Content":
                ContentPickerConversion.ConvertValues(context.Step["data"] ?? context.Step["Data"]);
                break;
        }

        return Task.CompletedTask;
    }

    public Task RecipeExecutingAsync(string executionId, RecipeDescriptor descriptor) => Task.CompletedTask;

    public Task RecipeStepExecutedAsync(RecipeExecutionContext context) => Task.CompletedTask;

    public Task RecipeExecutedAsync(string executionId, RecipeDescriptor descriptor) => Task.CompletedTask;

    public Task ExecutionFailedAsync(string executionId, RecipeDescriptor descriptor) => Task.CompletedTask;
}
