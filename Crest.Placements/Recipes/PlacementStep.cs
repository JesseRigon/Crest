using System.Text.Json.Nodes;
using Crest.DisplayManagement.Descriptors.ShapePlacementStrategy;
using Crest.Placements.Services;
using Crest.Recipes.Models;
using Crest.Recipes.Services;

namespace Crest.Placements.Recipes;

/// <summary>
/// This recipe step creates a set of placements.
/// </summary>
public sealed class PlacementStep : NamedRecipeStepHandler
{
    private readonly PlacementsManager _placementsManager;

    public PlacementStep(PlacementsManager placementsManager)
        : base("Placements")
    {
        _placementsManager = placementsManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step.TryGetPropertyValue("Placements", out var jsonNode) && jsonNode is JsonObject templates)
        {
            foreach (var property in templates)
            {
                var name = property.Key;
                var value = property.Value.ToObject<PlacementNode[]>();

                await _placementsManager.UpdateShapePlacementsAsync(name, value);
            }
        }
    }
}
