using System.Security.Claims;
using Crest.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Parties.Controllers;

/// <summary>
/// "My connections": the associations the signed-in user's Person may see. The reading
/// API is where the association rule is enforced: a rep sees only their own connections,
/// and the far end sees one only when its type allows it. Writes go through the content
/// items API like any other content.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crest/parties/associations")]
public sealed class PartyAssociationsController(
    IPartyUserLinkService partyUserLink,
    IPartyAssociationService associations) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<AssociationModel>>> MineAsync()
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
