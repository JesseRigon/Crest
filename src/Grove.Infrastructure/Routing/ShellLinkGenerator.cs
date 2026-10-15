using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Grove.Routing;

/// <summary>
/// The tenant's <see cref="IShellLinkGenerator"/>: <see cref="LinkGenerator"/> over the current
/// request (ambient route values, path base, scheme and host).
/// </summary>
internal sealed class ShellLinkGenerator(IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator) : IShellLinkGenerator
{
    private HttpContext HttpContext => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("Links are generated for a request: no HttpContext is available.");

    public string? GetPathByRouteValues(RouteValueDictionary values, string? routeName = null) =>
        linkGenerator.GetPathByRouteValues(HttpContext, routeName, values);

    public string? GetUriByRouteValues(RouteValueDictionary values, string? routeName = null) =>
        linkGenerator.GetUriByRouteValues(HttpContext, routeName, values);

    public string? GetPathByName(string endpointName, RouteValueDictionary? values = null) =>
        linkGenerator.GetPathByName(HttpContext, endpointName, values);

    public string Content(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Outside a request (a recipe, a background task) there is no path base to add.
        var pathBase = httpContextAccessor.HttpContext?.Request.PathBase ?? PathString.Empty;

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            return pathBase.Add(new PathString(path[1..])).Value ?? path[1..];
        }

        if (path == "~")
        {
            return pathBase.HasValue ? pathBase.Value! : "/";
        }

        return path;
    }

    public string ToAbsoluteUrl(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return path;
        }

        var request = HttpContext.Request;
        var local = path.StartsWith('~') ? Content(path) : path.StartsWith('/') ? path : "/" + path;
        return $"{request.Scheme}://{request.Host.ToUriComponent()}{local}";
    }

    public bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        // "/" or "/x" but not "//" or "/\"; "~/" or "~/x" but not "~//" or "~/\".
        if (url[0] == '/')
        {
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\' && !HasControlCharacter(url.AsSpan(1)));
        }

        if (url.Length > 1 && url[0] == '~' && url[1] == '/')
        {
            return url.Length == 2 || (url[2] != '/' && url[2] != '\\' && !HasControlCharacter(url.AsSpan(2)));
        }

        return false;
    }

    private static bool HasControlCharacter(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
