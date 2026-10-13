using System.Text.Json.Nodes;
using Crest.DisplayManagement.Components;
using Crest.Layers.Models;
using Crest.Layers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Layers.Controllers;

/// <summary>
/// The admin Layers page's API (rework step 6): the layers with their rules, and the component
/// instances they place. A placement is checked against the component's schema on the server
/// before it is stored (security audit item 7), as a template is.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/layers")]
public sealed class LayersApiController(
    ILayerService layers,
    LayerRuleReader rules,
    IComponentRegistryManager registries,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LayersModel>> GetAsync()
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

    [HttpPut("{name}")]
    public async Task<ActionResult<LayerModel>> SaveLayerAsync(string name, [FromBody] LayerModel model)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManageLayers))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new LayersError(["A layer needs a name."]));
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

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteLayerAsync(string name)
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

    [HttpPut("placements/{id}")]
    public async Task<ActionResult<LayerPlacement>> SavePlacementAsync(string id, [FromBody] LayerPlacement placement)
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

        // Layers fill the site's pages: the site shell's registry decides what may be placed.
        if (!registries.GetRegistry(ShellKind.Site).TryGet(placement.Component, out var descriptor))
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

    [HttpDelete("placements/{id}")]
    public async Task<IActionResult> DeletePlacementAsync(string id)
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
