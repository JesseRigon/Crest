using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.DisplayManagement.Descriptors.ShapePlacementStrategy;
using Grove.Placements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grove.Placements.Controllers;

/// <summary>
/// The Placements page's API (docs/blazor-display.md rework step 6: "its editor becomes a Blazor
/// page"): the tenant's placement rules per shape type, in the placement.json format, layered
/// over the modules' and the theme's own placement.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/grove/placements")]
public sealed class PlacementsApiController(PlacementsManager placements, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, JsonArray>>> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManagePlacements))
        {
            return Forbid();
        }

        var all = await placements.ListShapePlacementsAsync();
        return Ok(all.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToNode(pair.Value) as JsonArray ?? []));
    }

    [HttpPut("{shapeType}")]
    public async Task<IActionResult> SaveAsync(string shapeType, [FromBody] JsonArray rules)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManagePlacements))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(shapeType))
        {
            return BadRequest(new { errors = new[] { "A shape type is required." } });
        }

        PlacementNode[]? nodes;
        try
        {
            nodes = rules.Deserialize<PlacementNode[]>();
        }
        catch (JsonException exception)
        {
            return BadRequest(new { errors = new[] { exception.Message } });
        }

        if (nodes is null || nodes.Any(node => node is null))
        {
            return BadRequest(new { errors = new[] { "Every rule must be a placement object." } });
        }

        await placements.UpdateShapePlacementsAsync(shapeType.Trim(), nodes);
        return NoContent();
    }

    [HttpDelete("{shapeType}")]
    public async Task<IActionResult> DeleteAsync(string shapeType)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManagePlacements))
        {
            return Forbid();
        }

        await placements.RemoveShapePlacementsAsync(shapeType);
        return NoContent();
    }
}
