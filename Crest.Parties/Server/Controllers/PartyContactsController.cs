using Crest.Parties.Constants;
using Crest.Parties.Services;
using Crest.Parties.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security.Permissions;

namespace Crest.Parties.Controllers;

/// <summary>
/// Contact points and addresses of one party. These live INSIDE the party document
/// (BagParts), which the generic content-items editor round-trips but cannot edit,
/// so this is the write surface. Authorization is Orchard's own content permissions
/// evaluated against the PARTY item - a caller who may edit the party may edit its
/// contact data, nothing more granular.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/parties/{partyId}")]
public sealed class PartyContactsController(
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IPartyContactsService contacts) : ControllerBase
{
    [HttpGet("contacts")]
    public async Task<ActionResult<PartyContactsModel>> GetAsync(string partyId)
    {
        var (party, failure) = await LoadPartyAsync(partyId, CommonPermissions.ViewContent);
        if (failure is not null) return failure;

        return Ok(await contacts.ReadAsync(party!, HttpContext.RequestAborted));
    }

    [HttpPost("contact-points")]
    public Task<ActionResult<ContactPointModel>> AddContactPointAsync(string partyId, ContactPointWriteModel write)
        => WriteAsync(partyId, async party => (ContactPointModel?)await contacts.AddContactPointAsync(party, write, HttpContext.RequestAborted));

    [HttpPut("contact-points/{contactPointId}")]
    public Task<ActionResult<ContactPointModel>> UpdateContactPointAsync(string partyId, string contactPointId, ContactPointWriteModel write)
        => WriteAsync(partyId, party => contacts.UpdateContactPointAsync(party, contactPointId, write, HttpContext.RequestAborted));

    [HttpDelete("contact-points/{contactPointId}")]
    public Task<IActionResult> RemoveContactPointAsync(string partyId, string contactPointId)
        => RemoveAsync(partyId, party => contacts.RemoveContactPointAsync(party, contactPointId, HttpContext.RequestAborted));

    [HttpPost("addresses")]
    public Task<ActionResult<AddressModel>> AddAddressAsync(string partyId, AddressWriteModel write)
        => WriteAsync(partyId, async party => (AddressModel?)await contacts.AddAddressAsync(party, write, HttpContext.RequestAborted));

    [HttpPut("addresses/{addressId}")]
    public Task<ActionResult<AddressModel>> UpdateAddressAsync(string partyId, string addressId, AddressWriteModel write)
        => WriteAsync(partyId, party => contacts.UpdateAddressAsync(party, addressId, write, HttpContext.RequestAborted));

    [HttpDelete("addresses/{addressId}")]
    public Task<IActionResult> RemoveAddressAsync(string partyId, string addressId)
        => RemoveAsync(partyId, party => contacts.RemoveAddressAsync(party, addressId, HttpContext.RequestAborted));

    private async Task<ActionResult<T>> WriteAsync<T>(string partyId, Func<ContentItem, Task<T?>> write) where T : class
    {
        var (party, failure) = await LoadPartyAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        try
        {
            var result = await write(party!);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private async Task<IActionResult> RemoveAsync(string partyId, Func<ContentItem, Task<bool>> remove)
    {
        var (party, failure) = await LoadPartyAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        return await remove(party!) ? NoContent() : NotFound();
    }

    private Task<(ContentItem? Party, ActionResult? Failure)> LoadPartyAsync(string partyId, Permission permission) =>
        PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, permission,
            PartiesConstants.ContentTypes.Person, PartiesConstants.ContentTypes.Organization);
}
