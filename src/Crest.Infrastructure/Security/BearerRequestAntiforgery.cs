using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Crest.Security;

/// <summary>
/// The tenant's antiforgery service, except that a request authenticated by a bearer token is
/// always valid (docs/machine-actors.md). Antiforgery protects ambient credentials - cookies a
/// browser attaches on its own; a browser never attaches a bearer token on its own, so such a
/// request cannot be forged cross-site. Every validation path goes through
/// <see cref="IAntiforgery"/> (the MVC filters, the endpoint middleware, the workflow API gate),
/// so this one seam covers them all.
/// </summary>
public sealed class BearerRequestAntiforgery(IAntiforgery inner) : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => inner.GetAndStoreTokens(httpContext);

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => inner.GetTokens(httpContext);

    public Task<bool> IsRequestValidAsync(HttpContext httpContext) =>
        IsBearerRequest(httpContext.Request) ? Task.FromResult(true) : inner.IsRequestValidAsync(httpContext);

    public Task ValidateRequestAsync(HttpContext httpContext) =>
        IsBearerRequest(httpContext.Request) ? Task.CompletedTask : inner.ValidateRequestAsync(httpContext);

    public void SetCookieTokenAndHeader(HttpContext httpContext) => inner.SetCookieTokenAndHeader(httpContext);

    /// <summary>A request carrying <c>Authorization: Bearer …</c>. Basic is excluded: a browser re-sends it on its own.</summary>
    public static bool IsBearerRequest(HttpRequest request) =>
        request.Headers.TryGetValue(HeaderNames.Authorization, out var values)
        && values.Count == 1
        && values[0] is { } value
        && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        && value.Length > "Bearer ".Length;
}
