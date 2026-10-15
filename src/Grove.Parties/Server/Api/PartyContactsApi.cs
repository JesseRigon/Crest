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
/// <c>api/grove/parties/{partyId}</c>: contact points and addresses of one party. These live
/// INSIDE the party document (BagParts), which the generic content-items editor round-trips but
/// cannot edit, so this is the write surface. Authorization is Grove's own content permissions
/// evaluated against the PARTY item: a caller who may edit the party may edit its contact data,
/// nothing more granular.
/// </summary>
public sealed class PartyContactsApi : ApiContributor
{
    public override string Name => "PartyContacts";

    public override string FeatureId => "Grove.Parties";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("parties/{partyId}");
        routes.MapGet("contacts", (HttpContext http, [FromRoute] string partyId) => ApiHandlers.For<PartyContactsHandlers>(http).GetAsync(partyId));
        routes.MapPost("contact-points", (HttpContext http, [FromRoute] string partyId, [FromBody] ContactPointWriteModel write) => ApiHandlers.For<PartyContactsHandlers>(http).AddContactPointAsync(partyId, write));
        routes.MapPut("contact-points/{contactPointId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string contactPointId, [FromBody] ContactPointWriteModel write) => ApiHandlers.For<PartyContactsHandlers>(http).UpdateContactPointAsync(partyId, contactPointId, write));
        routes.MapDelete("contact-points/{contactPointId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string contactPointId) => ApiHandlers.For<PartyContactsHandlers>(http).RemoveContactPointAsync(partyId, contactPointId));
        routes.MapPost("addresses", (HttpContext http, [FromRoute] string partyId, [FromBody] AddressWriteModel write) => ApiHandlers.For<PartyContactsHandlers>(http).AddAddressAsync(partyId, write));
        routes.MapPut("addresses/{addressId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string addressId, [FromBody] AddressWriteModel write) => ApiHandlers.For<PartyContactsHandlers>(http).UpdateAddressAsync(partyId, addressId, write));
        routes.MapDelete("addresses/{addressId}", (HttpContext http, [FromRoute] string partyId, [FromRoute] string addressId) => ApiHandlers.For<PartyContactsHandlers>(http).RemoveAddressAsync(partyId, addressId));
    }
}

public sealed class PartyContactsHandlers(
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IPartyContactsService contacts) : ApiHandlers
{
    public async Task<IResult> GetAsync(string partyId)
    {
        var (party, failure) = await LoadPartyAsync(partyId, CommonPermissions.ViewContent);
        if (failure is not null) return failure;

        return Ok(await contacts.ReadAsync(party!, HttpContext.RequestAborted));
    }

    public Task<IResult> AddContactPointAsync(string partyId, ContactPointWriteModel write)
        => WriteAsync(partyId, async party => (ContactPointModel?)await contacts.AddContactPointAsync(party, write, HttpContext.RequestAborted));

    public Task<IResult> UpdateContactPointAsync(string partyId, string contactPointId, ContactPointWriteModel write)
        => WriteAsync(partyId, party => contacts.UpdateContactPointAsync(party, contactPointId, write, HttpContext.RequestAborted));

    public Task<IResult> RemoveContactPointAsync(string partyId, string contactPointId)
        => RemoveAsync(partyId, party => contacts.RemoveContactPointAsync(party, contactPointId, HttpContext.RequestAborted));

    public Task<IResult> AddAddressAsync(string partyId, AddressWriteModel write)
        => WriteAsync(partyId, async party => (AddressModel?)await contacts.AddAddressAsync(party, write, HttpContext.RequestAborted));

    public Task<IResult> UpdateAddressAsync(string partyId, string addressId, AddressWriteModel write)
        => WriteAsync(partyId, party => contacts.UpdateAddressAsync(party, addressId, write, HttpContext.RequestAborted));

    public Task<IResult> RemoveAddressAsync(string partyId, string addressId)
        => RemoveAsync(partyId, party => contacts.RemoveAddressAsync(party, addressId, HttpContext.RequestAborted));

    private async Task<IResult> WriteAsync<T>(string partyId, Func<ContentItem, Task<T?>> write) where T : class
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
            return BadRequest(PartyRequests.Errors(ex.Message));
        }
    }

    private async Task<IResult> RemoveAsync(string partyId, Func<ContentItem, Task<bool>> remove)
    {
        var (party, failure) = await LoadPartyAsync(partyId, CommonPermissions.EditContent);
        if (failure is not null) return failure;

        return await remove(party!) ? NoContent() : NotFound();
    }

    private Task<(ContentItem? Party, IResult? Failure)> LoadPartyAsync(string partyId, Permission permission) =>
        PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, permission,
            PartiesConstants.ContentTypes.Person, PartiesConstants.ContentTypes.Organization);
}
