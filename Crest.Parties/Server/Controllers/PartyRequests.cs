using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;
using OrchardCore.Security.Permissions;

namespace Crest.Parties.Controllers;

/// <summary>Shared "load this party and authorize the caller against it" step of the
/// party API controllers. 404 for a missing item or a non-party type (so the API
/// never confirms the existence of other content), 403 when Orchard refuses.</summary>
internal static class PartyRequests
{
    public static async Task<(ContentItem? Party, ActionResult? Failure)> LoadAsync(
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        string partyId,
        Permission permission,
        params string[] allowedContentTypes)
    {
        var party = await contentManager.GetAsync(partyId, VersionOptions.Latest);
        if (party is null || !allowedContentTypes.Contains(party.ContentType, StringComparer.Ordinal))
        {
            return (null, new NotFoundResult());
        }

        if (!await authorizationService.AuthorizeAsync(user, permission, party))
        {
            return (null, new ForbidResult());
        }

        return (party, null);
    }
}
