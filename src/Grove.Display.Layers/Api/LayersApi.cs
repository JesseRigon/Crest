using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Display.Management.Components;
using Grove.Display.Layers.Models;
using Grove.Display.Layers.Services;
using Grove.Display.Rules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Display.Layers.Api;

/// <summary>
/// <c>api/grove/layers</c>: the admin Layers page's API (rework step 6): the layers with their
/// rules, and the component instances they place. A placement is checked against the component's
/// schema on the server before it is stored (security audit item 7), as a template is.
/// </summary>
public sealed class LayersApi : ApiContributor
{
    public override string Name => "Layers";

    public override string FeatureId => "Grove.Display.Layers";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("layers");
        routes.MapGet("", ApiHandlers.Handle<LayersHandlers>(h => h.GetAsync()));
        routes.MapPut("placements/{id}", (HttpContext http, [FromRoute] string id, [FromBody] LayerPlacement placement) => ApiHandlers.For<LayersHandlers>(http).SavePlacementAsync(id, placement));
        routes.MapDelete("placements/{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<LayersHandlers>(http).DeletePlacementAsync(id));
        routes.MapPut("{name}", (HttpContext http, [FromRoute] string name, [FromBody] LayerModel model) => ApiHandlers.For<LayersHandlers>(http).SaveLayerAsync(name, model));
        routes.MapDelete("{name}", (HttpContext http, [FromRoute] string name) => ApiHandlers.For<LayersHandlers>(http).DeleteLayerAsync(name));
    }
}

public sealed class LayersHandlers(
    ILayerService layers,
    LayerRuleReader rules,
    IComponentRegistryManager registries,
    IEnumerable<IRuleConditionChecker> conditionCheckers,
    IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageLayers))
        {
            return Forbid();
        }

        var document = await layers.GetLayersAsync();
        return Ok(new LayersModel(
            document.Layers.Select(layer => new LayerModel(layer.Name, layer.Description, rules.Write(layer.LayerRule))).ToArray(),
            document.Placements.OrderBy(placement => placement.Zone).ThenBy(placement => placement.Position).ToArray()));
    }

    public async Task<IResult> SaveLayerAsync(string name, LayerModel model)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageLayers))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new LayersError(["A layer needs a name."]));
        }

        // The rule editor's checks, run again on save (Grove.Display.Rules' checker: unknown
        // condition types, a string condition without its operator, a JavaScript condition that is
        // empty, does not parse, throws or is not a boolean), keyed by wire name:
        // conditions.{conditionId}.
        if (conditionCheckers.FirstOrDefault() is { } checker
            && await checker.CheckAsync(model.Conditions) is { Count: > 0 } problems)
        {
            return ApiProblems.Validation(
                problems.ToDictionary(problem => $"conditions.{problem.Key}", problem => new[] { problem.Value }),
                detail: "Fix the layer's conditions.");
        }

        var unknown = new List<string>();
        var rule = rules.Read(model.Conditions, unknown);
        if (unknown.Count > 0)
        {
            return BadRequest(new LayersError([$"These conditions are not available (is their feature enabled?): {string.Join(", ", unknown)}."]));
        }

        var document = await layers.LoadLayersAsync();
        var layer = document.Layers.FirstOrDefault(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));
        if (layer is null)
        {
            document.Layers.Add(layer = new Layer { Name = name.Trim() });
        }

        layer.Description = model.Description;
        layer.LayerRule = rule;
        await layers.UpdateAsync(document);
        return Ok(new LayerModel(layer.Name, layer.Description, rules.Write(rule)));
    }

    public async Task<IResult> DeleteLayerAsync(string name)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageLayers))
        {
            return Forbid();
        }

        var document = await layers.LoadLayersAsync();
        if (document.Placements.Any(placement => string.Equals(placement.Layer, name, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new LayersError(["The layer still places components; remove them first."]));
        }

        document.Layers.RemoveAll(layer => string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase));
        await layers.UpdateAsync(document);
        return NoContent();
    }

    public async Task<IResult> SavePlacementAsync(string id, LayerPlacement placement)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageLayers))
        {
            return Forbid();
        }

        var document = await layers.LoadLayersAsync();
        var errors = new List<string>();
        if (!document.Layers.Any(layer => string.Equals(layer.Name, placement.Layer, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"There is no layer '{placement.Layer}'.");
        }

        if (string.IsNullOrWhiteSpace(placement.Zone))
        {
            errors.Add("A placement needs the zone key it fills.");
        }

        // Layers fill the site's pages: the site shell's registry decides what may be placed,
        // and where (decision 6: an unknown zone key is a save error, as is a component the key
        // does not admit).
        var registry = registries.GetRegistry(ShellKind.Site);
        if (!string.IsNullOrWhiteSpace(placement.Zone))
        {
            if (!registry.TryGetZoneKey(placement.Zone, out var zoneKey))
            {
                errors.Add($"'{placement.Zone}' is not a declared zone key.");
            }
            else if (!zoneKey.Admits(placement.Component))
            {
                errors.Add($"The zone '{placement.Zone}' does not admit '{placement.Component}'.");
            }
        }

        if (!registry.TryGet(placement.Component, out var descriptor))
        {
            errors.Add($"'{placement.Component}' is not a registered component.");
        }
        else
        {
            ComponentParameters.Bind(descriptor, (placement.Properties ?? []).Select(pair => KeyValuePair.Create(pair.Key, (object?)pair.Value?.DeepClone())), errors);
        }

        if (errors.Count > 0)
        {
            return BadRequest(new LayersError([.. errors]));
        }

        placement.Id = string.IsNullOrWhiteSpace(id) || id == "new" ? Guid.NewGuid().ToString("n") : id;
        placement.Properties ??= [];
        document.Placements.RemoveAll(existing => string.Equals(existing.Id, placement.Id, StringComparison.Ordinal));
        document.Placements.Add(placement);
        await layers.UpdateAsync(document);
        return Ok(placement);
    }

    public async Task<IResult> DeletePlacementAsync(string id)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageLayers))
        {
            return Forbid();
        }

        var document = await layers.LoadLayersAsync();
        document.Placements.RemoveAll(placement => string.Equals(placement.Id, id, StringComparison.Ordinal));
        await layers.UpdateAsync(document);
        return NoContent();
    }
}

public sealed record LayerModel(string Name, string? Description, JsonArray? Conditions);

public sealed record LayersModel(LayerModel[] Layers, LayerPlacement[] Placements);

public sealed record LayersError(string[] Errors);
