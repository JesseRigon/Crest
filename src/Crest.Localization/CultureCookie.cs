using Microsoft.AspNetCore.Http;
using Crest.Environment.Shell;

namespace Crest.Localization;

/// <summary>
/// The tenant-wide culture cookie: one cookie per tenant, written by whichever surface resolves
/// the culture (the Blazor client resolves the full chain itself and writes the winner), read by
/// the one <c>CookieRequestCultureProvider</c> the platform registers for every request.
/// </summary>
public static class CultureCookie
{
    public const string CookieNamePrefix = "culture_";

    public static string MakeCookieName(ShellSettings shellSettings) => CookieNamePrefix + shellSettings.VersionId;

    public static string MakeCookiePath(HttpContext httpContext) => httpContext.Request.PathBase.HasValue
        ? httpContext.Request.PathBase.ToString()
        : "/";
}
