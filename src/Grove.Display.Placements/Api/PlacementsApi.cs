using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Display.Management.Descriptors.ShapePlacementStrategy;
using Grove.Display.Placements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Display.Placements.Api;

/// <summary>
/// <c>api/grove/placements</c>: the Placements page's API (docs/blazor-display.md rework step 6):
/// the tenant's placement rules per shape type, in the placement.json format, layered over the
/// modules' and the theme's own placement. Every endpoint needs <c>ManagePlacements</c>.
/// </summary>
public sealed class PlacementsApi : ApiContributor
{
    public override string Name => "Placements";

    public override string FeatureId => "Grove.Display.Placements";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("placements");
        routes.MapGet("", ApiHandlers.Handle<PlacementsHandlers>(h => h.ListAsync()));
        routes.MapPut("{shapeType}", (HttpContext http, [FromRoute] string shapeType, [FromBody] JsonArray rules) => ApiHandlers.For<PlacementsHandlers>(http).SaveAsync(shapeType, rules));
        routes.MapDelete("{shapeType}", (HttpContext http, [FromRoute] string shapeType) => ApiHandlers.For<PlacementsHandlers>(http).DeleteAsync(shapeType));
    }
}

public sealed class PlacementsHandlers(PlacementsManager placements, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManagePlacements))
        {
            return Forbid();
        }

        var all = await placements.ListShapePlacementsAsync();
        return Ok(all.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToNode(pair.Value) as JsonArray ?? []));
    }

    public async Task<IResult> SaveAsync(string shapeType, JsonArray rules)
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

    public async Task<IResult> DeleteAsync(string shapeType)
    {
        if (!await authorization.AuthorizeAsync(User, Permissions.ManagePlacements))
        {
            return Forbid();
        }

        await placements.RemoveShapePlacementsAsync(shapeType);
        return NoContent();
    }
}
