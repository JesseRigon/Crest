using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Json;
using Crest.Layers.Models;
using Crest.Layers.Services;
using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.Rules;
using Crest.Rules.Services;

namespace Crest.Layers.Recipes;

/// <summary>
/// Creates or updates layers and their placements: <c>{ "name": "Layers", "Layers": [ { "Name",
/// "Description", "LayerRule": { "Conditions": [ { "Name": "BooleanCondition", "Value": true } ] } } ],
/// "Placements": [ { "Id", "Layer", "Zone", "Component", "Properties", "Position" } ] }</c>. A
/// placement with the id of an existing one replaces it.
/// </summary>
public sealed class LayerStep(ILayerService layerService, LayerRuleReader rules, IStringLocalizer<LayerStep> stringLocalizer) : NamedRecipeStepHandler("Layers")
{
    internal readonly IStringLocalizer S = stringLocalizer;

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<LayersStepModel>();
        var allLayers = await layerService.LoadLayersAsync();
        var unknownTypes = new List<string>();

        foreach (var layerStep in model.Layers ?? [])
        {
            if (string.IsNullOrEmpty(layerStep.Name))
            {
                context.Errors.Add(S["A layer needs a name."]);
                continue;
            }

            var layer = allLayers.Layers.FirstOrDefault(x => string.Equals(x.Name, layerStep.Name, StringComparison.OrdinalIgnoreCase));
            if (layer == null)
            {
                layer = new Layer { Name = layerStep.Name };
                allLayers.Layers.Add(layer);
            }

            // The conditions are replaced, because conditions cannot be merged logically.
            if (layerStep.LayerRule != null)
            {
                layer.LayerRule = rules.Read(layerStep.LayerRule.Conditions, unknownTypes);
                if (!string.IsNullOrEmpty(layerStep.LayerRule.ConditionId))
                {
                    layer.LayerRule.ConditionId = layerStep.LayerRule.ConditionId;
                }
            }

            layer.LayerRule ??= rules.Read(null, unknownTypes);

            if (!string.IsNullOrEmpty(layerStep.Description))
            {
                layer.Description = layerStep.Description;
            }
        }

        foreach (var placement in model.Placements ?? [])
        {
            if (string.IsNullOrWhiteSpace(placement.Layer) || string.IsNullOrWhiteSpace(placement.Zone) || string.IsNullOrWhiteSpace(placement.Component))
            {
                context.Errors.Add(S["A layer placement needs its layer, zone and component."]);
                continue;
            }

            if (string.IsNullOrWhiteSpace(placement.Id))
            {
                placement.Id = Guid.NewGuid().ToString("n");
            }

            allLayers.Placements.RemoveAll(existing => string.Equals(existing.Id, placement.Id, StringComparison.Ordinal));
            allLayers.Placements.Add(placement);
        }

        if (unknownTypes.Count != 0)
        {
            context.Errors.Add(S["No changes have been made. The following types of conditions cannot be added: {0}. Please ensure that the related features are enabled to add these types of conditions.", string.Join(", ", unknownTypes)]);
            return;
        }

        await layerService.UpdateAsync(allLayers);
    }
}

public class LayersStepModel
{
    public LayerStepModel[] Layers { get; set; } = [];

    public LayerPlacement[] Placements { get; set; }
}

public class LayerStepModel
{
    public string Name { get; set; }

    public string Rule { get; set; }
    public string Description { get; set; }

    public RuleStepModel LayerRule { get; set; }
}

public class RuleStepModel
{
    public string Name { get; set; }
    public string ConditionId { get; set; }
    public JsonArray Conditions { get; set; }
}
