using Crest.DisplayManagement;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Layout;
using Crest.Layers.Models;
using Crest.Rules;
using Crest.Rules.Services;
using Microsoft.Extensions.Logging;

namespace Crest.Layers.Services;

/// <summary>
/// Layers as a build step (docs/blazor-display.md rework step 6): each layer's rule is evaluated
/// once per page, and every placement of an admitted layer becomes a shape of its component's
/// name in its zone, ordered by position. The tree builder maps it like any other shape (the
/// schema checks its values, the component's permission and the node filter apply), so a layer
/// contribution is a node of the page's one tree. A placement naming a component the registry
/// does not have is skipped and logged.
/// </summary>
public sealed class LayerTreeContributor(
    ILayerService layerService,
    IRuleService ruleService,
    IShapeFactory shapeFactory,
    IComponentRegistry registry,
    ILogger<LayerTreeContributor> logger) : ILayoutTreeContributor
{
    public int Order => 100;

    public async Task ContributeAsync(LayoutTreeContext context, CancellationToken cancellationToken = default)
    {
        var document = await layerService.GetLayersAsync();
        if (document.Placements.Count == 0)
        {
            return;
        }

        var admitted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in document.Layers)
        {
            admitted[layer.Name] = layer.LayerRule is { } rule && await ruleService.EvaluateAsync(rule);
        }

        foreach (var placement in document.Placements.OrderBy(placement => placement.Position))
        {
            if (!admitted.GetValueOrDefault(placement.Layer) || string.IsNullOrEmpty(placement.Zone))
            {
                continue;
            }

            if (!registry.TryGet(placement.Component, out var descriptor))
            {
                logger.LogWarning("The layer placement '{Placement}' names '{Component}', which is not a registered component of this shell.", placement.Id, placement.Component);
                continue;
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in ComponentParameters.Bind(descriptor, placement.Properties.Select(pair => KeyValuePair.Create(pair.Key, (object?)pair.Value?.DeepClone()))))
            {
                values[name] = value;
            }

            var shape = await shapeFactory.CreateAsync(descriptor.Name, Arguments.From(values));
            shape.Metadata.Name = placement.Id;
            await context.Layout.Zones[placement.Zone].AddAsync(shape, placement.Position.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
