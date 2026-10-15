using Grove.ContentManagement;
using Grove.Contents;
using Grove.Parties.Constants;
using Grove.Parties.Services;
using Grove.Parties.ViewModels;
using Grove.Security.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grove.Parties.Controllers;

/// <summary>An organization's legal entity names (inside the organization document, so
/// this is their write surface, like contact points). Authorized against the
/// organization: whoever may edit it may edit its names.</summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/grove/parties/{partyId}/names")]
public sealed class PartyNamesController(
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IPartyNamesService names) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationNameModel>>> GetAsync(string partyId)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.ViewContent);
        if (failure is not null) return failure;

        return Ok(await names.ReadAsync(organization!, HttpContext.RequestAborted));
    }

    [HttpPost]
    public async Task<ActionResult<OrganizationNameModel>> AddAsync(string partyId, OrganizationNameWriteModel write)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            return Ok(await names.AddAsync(organization!, write, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPut("{nameId}")]
    public async Task<ActionResult<OrganizationNameModel>> UpdateAsync(string partyId, string nameId, OrganizationNameWriteModel write)
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
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpDelete("{nameId}")]
    public async Task<IActionResult> RemoveAsync(string partyId, string nameId)
    {
        var (organization, failure) = await LoadAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        return await names.RemoveAsync(organization!, nameId, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    private Task<(ContentItem? Party, ActionResult? Failure)> LoadAsync(string partyId, Permission permission) =>
        PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, permission, PartiesConstants.ContentTypes.Organization);
}
