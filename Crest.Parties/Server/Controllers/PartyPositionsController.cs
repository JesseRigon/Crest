using Crest.Parties.Constants;
using Crest.Parties.Services;
using Crest.Parties.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace Crest.Parties.Controllers;

/// <summary>
/// Org structure: the positions a Person holds (written on the person) and, from the
/// organization side, who holds positions there (read through the position index).
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/parties/{partyId}")]
public sealed class PartyPositionsController(
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IPartyPositionsService positions) : ControllerBase
{
    [HttpGet("positions")]
    public async Task<ActionResult<IReadOnlyList<PositionModel>>> GetAsync(string partyId)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.ViewContent);
        if (failure is not null) return failure;

        return Ok(await positions.ReadAsync(person!, HttpContext.RequestAborted));
    }

    [HttpPost("positions")]
    public async Task<ActionResult<PositionModel>> AddAsync(string partyId, PositionWriteModel write)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            return Ok(await positions.AddAsync(person!, write, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPut("positions/{positionId}")]
    public async Task<ActionResult<PositionModel>> UpdateAsync(string partyId, string positionId, PositionWriteModel write)
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
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpDelete("positions/{positionId}")]
    public async Task<IActionResult> RemoveAsync(string partyId, string positionId)
    {
        var (person, failure) = await LoadPersonAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        return await positions.RemoveAsync(person!, positionId, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    /// <summary>Who holds positions in this organization. Viewing the organization
    /// is the permission that gates it.</summary>
    [HttpGet("people")]
    public async Task<ActionResult<IReadOnlyList<OrganizationPersonModel>>> PeopleAsync(string partyId)
    {
        var (organization, failure) = await PartyRequests.LoadAsync(
            contentManager, authorizationService, User, partyId, CommonPermissions.ViewContent, PartiesConstants.ContentTypes.Organization);
        if (failure is not null) return failure;

        return Ok(await positions.ListPeopleAsync(organization!.ContentItemId, HttpContext.RequestAborted));
    }

    private Task<(ContentItem? Party, ActionResult? Failure)> LoadPersonAsync(string partyId, OrchardCore.Security.Permissions.Permission permission) =>
        PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, permission, PartiesConstants.ContentTypes.Person);
}
