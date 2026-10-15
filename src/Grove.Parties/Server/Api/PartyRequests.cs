using System.Security.Claims;
using Grove.Contents.Management;
using Grove.Security.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Grove.Parties.Api;

/// <summary>Shared "load this party and authorize the caller against it" step of the
/// party APIs. 404 for a missing item or a non-party type (so the API never confirms the
/// existence of other content), 403 when Grove refuses.</summary>
internal static class PartyRequests
{
    public static async Task<(ContentItem? Party, IResult? Failure)> LoadAsync(
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
            return (null, TypedResults.NotFound());
        }

        if (!await authorizationService.AuthorizeAsync(user, permission, party))
        {
            return (null, TypedResults.Forbid());
        }

        return (party, null);
    }

    /// <summary>The body the party APIs answer a refused write with: <c>{ errors: [message] }</c>.</summary>
    public static object Errors(string message) => new { errors = new[] { message } };
}
