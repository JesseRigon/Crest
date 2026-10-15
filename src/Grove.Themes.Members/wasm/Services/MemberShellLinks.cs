using Grove.Display.Components.Modules;
using Microsoft.AspNetCore.Components;

namespace Grove.Themes.Members.Services;

/// <summary>
/// URLs inside the member shell, composed on the document's own base rather than a typed
/// prefix: an organization-free page's document base is the member base
/// (<c>{tenant}/{member prefix}/</c>), every other page's is the organization's
/// (<c>{tenant}/{member prefix}/{slug}/</c>).
/// </summary>
public static class MemberShellLinks
{
    /// <summary>A route relative to the current document base ("account", not "/account",
    /// which would escape the tenant and the shell).</summary>
    public static string Relative(string route) => route.TrimStart('/');

    /// <summary>The member shell's base URL, whichever document this is.</summary>
    public static string MemberBase(NavigationManager navigation, GroveShellContext? shell)
    {
        var baseUri = navigation.BaseUri;
        return shell?.OrganizationSlug is { Length: > 0 } slug
            && baseUri.EndsWith("/" + slug + "/", StringComparison.OrdinalIgnoreCase)
                ? baseUri[..^(slug.Length + 1)]
                : baseUri;
    }

    /// <summary>An organization-free page (sign-in, the picker) from any member document.</summary>
    public static string OrganizationFree(NavigationManager navigation, GroveShellContext? shell, string route) =>
        MemberBase(navigation, shell) + Relative(route);

    /// <summary>An organization's home, from any member document.</summary>
    public static string Organization(NavigationManager navigation, GroveShellContext? shell, string slug) =>
        MemberBase(navigation, shell) + Uri.EscapeDataString(slug) + "/";
}
