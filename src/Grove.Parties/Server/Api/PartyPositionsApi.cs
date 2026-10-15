using Grove.Api;
using Grove.Contents;
using Grove.Contents.Management;
using Grove.Parties.Constants;
using Grove.Parties.Services;
using Grove.Parties.ViewModels;
using Grove.Security.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Parties.Api;

/// <summary>
/// <c>api/grove/parties/{partyId}</c>, org structure: the positions a Person holds (written on
/// the person) and, from the organization side, who holds positions there (read through the
/// position index).
/// </summary>
public sealed class PartyPositionsApi : ApiContributor
{
    public override string Name => "PartyPositions";

    public override string FeatureId => "Grove.Parties";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("parties/{partyId}");
        routes.MapGet("positions", (HttpContext http, [FromRoute] string partyId) => ApiHandlers.For<PartyPositionsHandlers>(http).GetAsync(partyId));
        routes.MapPost("positions", (HttpContext http, [FromRoute] string partyId, [FromBody] PositionWriteModel write) => ApiHandlers.For<PartyPositionsHandlers>(http).AddAsync(partyId, write));
        routes.MapPut("positions/{positionId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string positionId, [FromBody] PositionWriteModel write) => ApiHandlers.For<PartyPositionsHandlers>(http).UpdateAsync(partyId, positionId, write));
        routes.MapDelete("positions/{positionId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string positionId) => ApiHandlers.For<PartyPositionsHandlers>(http).RemoveAsync(partyId, positionId));
        routes.MapGet("people", (HttpContext http, [FromRoute] string partyId) => ApiHandlers.For<PartyPositionsHandlers>(http).PeopleAsync(partyId));
    }
}

public sealed class PartyPositionsHandlers(
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IPartyPositionsService positions) : ApiHandlers
{
    public async Task<IResult> GetAsync(string partyId)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.ViewContent);
        if (failure is not null) return failure;

        return Ok(await positions.ReadAsync(person!, HttpContext.RequestAborted));
    }

    public async Task<IResult> AddAsync(string partyId, PositionWriteModel write)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            return Ok(await positions.AddAsync(person!, write, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PartyRequests.Errors(ex.Message));
        }
    }

    public async Task<IResult> UpdateAsync(string partyId, string positionId, PositionWriteModel write)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            var updated = await positions.UpdateAsync(person!, positionId, write, HttpContext.RequestAborted);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PartyRequests.Errors(ex.Message));
        }
    }

    public async Task<IResult> RemoveAsync(string partyId, string positionId)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        return await positions.RemoveAsync(person!, positionId, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    /// <summary>Who holds positions in this organization. Viewing the organization is the
    /// permission that gates it.</summary>
    public async Task<IResult> PeopleAsync(string partyId)
    {
        var (organization, failure) = await PartyRequests.LoadAsync(
            contentManager, authorizationService, User, partyId, CommonPermissions.ViewContent, PartiesConstants.ContentTypes.Organization);
        if (failure is not null) return failure;

        return Ok(await positions.ListPeopleAsync(organization!.ContentItemId, HttpContext.RequestAborted));
    }

    private Task<(ContentItem? Party, IResult? Failure)> LoadPersonAsync(string partyId, Permission permission) =>
        PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, permission, PartiesConstants.ContentTypes.Person);
}
