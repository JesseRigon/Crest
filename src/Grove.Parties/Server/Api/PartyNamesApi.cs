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

/// <summary><c>api/grove/parties/{partyId}/names</c>: an organization's legal entity names
/// (inside the organization document, so this is their write surface, like contact points).
/// Authorized against the organization: whoever may edit it may edit its names.</summary>
public sealed class PartyNamesApi : ApiContributor
{
    public override string Name => "PartyNames";

    public override string FeatureId => "Grove.Parties";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("parties/{partyId}/names");
        routes.MapGet("", (HttpContext http, [FromRoute] string partyId) => ApiHandlers.For<PartyNamesHandlers>(http).GetAsync(partyId));
        routes.MapPost("", (HttpContext http, [FromRoute] string partyId, [FromBody] OrganizationNameWriteModel write) => ApiHandlers.For<PartyNamesHandlers>(http).AddAsync(partyId, write));
        routes.MapPut("{nameId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string nameId, [FromBody] OrganizationNameWriteModel write) => ApiHandlers.For<PartyNamesHandlers>(http).UpdateAsync(partyId, nameId, write));
        routes.MapDelete("{nameId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string nameId) => ApiHandlers.For<PartyNamesHandlers>(http).RemoveAsync(partyId, nameId));
    }
}

public sealed class PartyNamesHandlers(
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IPartyNamesService names) : ApiHandlers
{
    public async Task<IResult> GetAsync(string partyId)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.ViewContent);
        if (failure is not null) return failure;

        return Ok(await names.ReadAsync(organization!, HttpContext.RequestAborted));
    }

    public async Task<IResult> AddAsync(string partyId, OrganizationNameWriteModel write)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            return Ok(await names.AddAsync(organization!, write, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PartyRequests.Errors(ex.Message));
        }
    }

    public async Task<IResult> UpdateAsync(string partyId, string nameId, OrganizationNameWriteModel write)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            var updated = await names.UpdateAsync(organization!, nameId, write, HttpContext.RequestAborted);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PartyRequests.Errors(ex.Message));
        }
    }

    public async Task<IResult> RemoveAsync(string partyId, string nameId)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        return await names.RemoveAsync(organization!, nameId, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    private Task<(ContentItem? Party, IResult? Failure)> LoadAsync(string partyId, Permission permission) =>
        PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, permission, PartiesConstants.ContentTypes.Organization);
}
