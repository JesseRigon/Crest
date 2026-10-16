using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Contents.Types.RecipeSteps;

/// <summary>
/// This recipe step deletes content definition records.
/// </summary>
public sealed class DeleteContentDefinitionStep : NamedRecipeStepHandler
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public DeleteContentDefinitionStep(IContentDefinitionManager contentDefinitionManager)
        : base("DeleteContentDefinition")
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var step = context.Step.ToObject<DeleteContentDefinitionStepModel>();

        foreach (var contentType in step.ContentTypes)
        {
            // The content definition manager tests existence before trying to delete.
            await _contentDefinitionManager.DeleteTypeDefinitionAsync(contentType);
        }

        foreach (var contentFieldSet in step.ContentFieldSets)
        {
            // The content definition manager tests existence before trying to delete.
            await _contentDefinitionManager.DeleteFieldSetDefinitionAsync(contentFieldSet);
        }
    }

    private sealed class DeleteContentDefinitionStepModel
    {
        public string[] ContentTypes { get; set; } = [];
        public string[] ContentFieldSets { get; set; } = [];
    }
}
