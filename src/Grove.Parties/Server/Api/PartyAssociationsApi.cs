using System.Security.Claims;
using Grove.Api;
using Grove.Parties.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Grove.Parties.Api;

/// <summary>
/// <c>api/grove/parties/associations</c>, "my connections": the associations the signed-in
/// user's Person may see. The reading API is where the association rule is enforced: a rep sees
/// only their own connections, and the far end sees one only when its type allows it. Writes go
/// through the content items API like any other content.
/// </summary>
public sealed class PartyAssociationsApi : ApiContributor
{
    public override string Name => "PartyAssociations";

    public override string FeatureId => "Grove.Parties";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("parties/associations").RequireAuthorization();
        routes.MapGet("me", ApiHandlers.Handle<PartyAssociationsHandlers>(h => h.MineAsync()));
    }
}

public sealed class PartyAssociationsHandlers(
    IPartyUserLinkService partyUserLink,
    IPartyAssociationService associations) : ApiHandlers
{
    public async Task<IResult> MineAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var personId = userId is null ? null : await partyUserLink.FindPersonIdForUserAsync(userId, HttpContext.RequestAborted);
        if (personId is null)
        {
            return Ok(Array.Empty<AssociationModel>());
        }

        return Ok(await associations.ListForPersonAsync(personId, HttpContext.RequestAborted));
    }
}
