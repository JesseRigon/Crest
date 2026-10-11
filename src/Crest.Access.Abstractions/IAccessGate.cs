using Microsoft.AspNetCore.Http;

namespace Crest.Access;

/// <summary>
/// Steps 3 and 4 of the request path (docs/access.md § One gate): the one place that
/// authenticates a request (the identity cookie, or the <c>Api</c> scheme when the request
/// carries an <c>Authorization</c> header), stamps the side the shell selector chose, builds
/// the caller and hands it to <see cref="ICallerContextAccessor"/>. Called once per request:
/// by the shell selector for the requests it classifies, else by the access middleware at the
/// authentication slot. Nothing else reads the principal or calls the caller factory.
/// </summary>
public interface IAccessGate
{
    /// <summary>
    /// Admits a request on the given side. A contributor may refuse (a member-side request
    /// for an organization the user holds no binding to): the response is written (403) and
    /// false returned. With <paramref name="allowAnonymous"/> false an anonymous request is
    /// refused too, without a response: the caller decides between a login redirect and 401.
    /// </summary>
    Task<bool> AdmitAsync(HttpContext context, CallerSide side, bool allowAnonymous, string? organizationId = null);

    /// <summary>
    /// Admits an API request. The shell prefix the call was addressed through
    /// (<paramref name="prefixSide"/>) decides its side and <c>X-Shell</c> may only confirm it
    /// (a disagreeing header is denied); a call with no prefix side takes the header's side;
    /// a cookie-authenticated one without either is malformed and denied; a bearer or
    /// anonymous one is Site.
    /// </summary>
    Task<bool> AdmitApiAsync(HttpContext context, CallerSide? prefixSide);
}
