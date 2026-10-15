using Microsoft.AspNetCore.Routing;

namespace Grove.Routing;

/// <summary>
/// Link generation for code that runs inside a tenant's request without MVC (docs/MVC/api.md ›
/// Plumbing): the tenant's <see cref="LinkGenerator"/> with the request's ambient values and the
/// tenant's path base, so the shell's own address schemes (<see cref="IShellRouteValuesAddressScheme"/>:
/// content item routes, the home route) take part as they did through <c>IUrlHelper</c>.
/// </summary>
public interface IShellLinkGenerator
{
    /// <summary>The path (with the tenant's path base) of the endpoint the route values address, or null.</summary>
    string? GetPathByRouteValues(RouteValueDictionary values, string? routeName = null);

    /// <summary>The absolute URL of the endpoint the route values address, or null.</summary>
    string? GetUriByRouteValues(RouteValueDictionary values, string? routeName = null);

    /// <summary>The path of a named endpoint (an endpoint's <c>WithName</c>), or null.</summary>
    string? GetPathByName(string endpointName, RouteValueDictionary? values = null);

    /// <summary>
    /// An application-relative path (<c>~/x</c>) under the tenant's path base, as
    /// <c>IUrlHelper.Content</c> did; anything else is returned unchanged.
    /// </summary>
    string Content(string path);

    /// <summary>
    /// An absolute URL (the request's scheme and host) for a path: <c>~/x</c> under the tenant's
    /// path base, <c>/x</c> as is (a generated path already carries the path base); an absolute
    /// http(s) URL is returned unchanged.
    /// </summary>
    string ToAbsoluteUrl(string path);

    /// <summary>Whether the URL is local to the application (<c>/x</c> or <c>~/x</c>, not <c>//host</c>).</summary>
    bool IsLocalUrl(string? url);
}
