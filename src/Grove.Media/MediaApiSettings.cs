using System.ComponentModel.DataAnnotations;

namespace Grove.Media;

/// <summary>
/// The authentication scheme used by the Media API and TUS upload endpoint. Exactly one is active
/// at a time so the Media Gallery works out of the box with the ambient admin cookie and can opt
/// into bearer tokens (OAuth2 + PKCE) once OpenID is configured. The media SignalR hub accepts
/// either scheme.
/// </summary>
public enum MediaApiAuthenticationScheme
{
    /// <summary>Same-origin admin cookie, with antiforgery validation on mutations. The default.</summary>
    [Display(Name = "Cookie (default)")]
    Cookie,

    /// <summary>OAuth2 bearer tokens via the "Api" scheme (requires OpenID Token Validation).</summary>
    [Display(Name = "Bearer — OAuth2 + PKCE (requires OpenID Connect)")]
    Bearer,
}

public class MediaApiSettings
{
    public const string GroupId = "mediaApi";

    [Display(Name = "Media API authentication", Description = "How the Media Gallery, uploads and realtime updates authenticate to the Media API (api/media). Exactly one scheme is active at a time — never both. Cookie works out of the box with the admin session (with antiforgery protection on writes). Bearer uses OAuth2 bearer tokens and requires the OpenID Connect Token Validation feature and a registered PKCE client.")]
    public MediaApiAuthenticationScheme AuthenticationScheme { get; set; }
}

public static class MediaApiConstants
{
    /// <summary>The authorization policy the Media API and TUS endpoints require.</summary>
    public const string AuthorizationPolicyName = "MediaApi";

    public const string HubAuthorizationPolicyName = "MediaHub";
}
