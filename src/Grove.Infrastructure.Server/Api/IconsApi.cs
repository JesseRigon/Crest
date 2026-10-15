using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Services;
using Microsoft.AspNetCore.Authorization;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/icons</c> endpoints.</summary>
public sealed class IconsApi : ApiContributor
{
    public override string Name => "Icons";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("icons");
        routes.MapGet("", (HttpContext http, [FromQuery] string? library, [FromQuery] string? query, [FromQuery] string[]? filter, [FromQuery] int skip = 0, [FromQuery] int take = 200) => ApiHandlers.For<IconsHandlers>(http).SearchAsync(library, query, filter, skip, take));
    }
}

public sealed class IconsHandlers(
    IAuthorizationService authorizationService,
    IconSourceStore iconSourceStore) : ApiHandlers
{
    public async Task<IResult> SearchAsync(string? library, string? query, string[]? filter, int skip = 0, int take = 200)
    {
        if (!await authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        return Ok(await iconSourceStore.SearchAsync(library, query, skip, take, ParseFilters(filter), HttpContext.RequestAborted));
    }

    private static GroveIconSearchFilter[] ParseFilters(string[]? filters) =>
        (filters ?? [])
            .Select(value => value.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            .Select(parts => new GroveIconSearchFilter(parts[0], parts[1]))
            .ToArray();
}
