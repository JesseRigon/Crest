namespace Crest.AdminTheme.Options;

// AdminPath/LoginPath/MemberPath hold the tenant's real, absolute shell URLs (e.g.
// "/backoffice"), fetched anonymously from RoutingController at boot (see
// Program.cs) so components (e.g. AdminTitleBar's logout redirect, Login.razor's
// post-login redirect) can build real, navigable cross-shell URLs without knowing
// about <base href> rewriting. The Canonical*Path constants below are only the
// fallback used if that fetch fails - they match AdminOptions.AdminUrlPrefix's own
// stock default ("Admin") and UserOptions.LoginPath's own stock default ("Login"),
// not some Crest-specific "canonical shape" - every @page directive in this app is
// itself already the real page path with no prefix baked in (Blazor's Router
// resolves them relative to BaseUri, which the server rewrites to the tenant's real
// prefix - see ShellSelectionMiddleware's PathBase shift).
public sealed class RoutingOptions
{
    public const string CanonicalAdminPath = "/Admin";
    public const string CanonicalLoginPath = "/login";

    public const string CanonicalMemberPath = "/members";

    /// <summary>
    /// The tenant's base path ("" or "/t2"), as the server addressed this request - never
    /// derived on the client. AdminPath, LoginPath and MemberPath are already composed on it;
    /// it is kept for any other tenant-absolute link (the shell switcher, a cross-shell URL).
    /// </summary>
    public string TenantBase { get; set; } = string.Empty;

    public string AdminPath { get; set; } = CanonicalAdminPath;
    public string LoginPath { get; set; } = CanonicalLoginPath;
    public string MemberPath { get; set; } = CanonicalMemberPath;

    /// <summary>
    /// The module client libraries enabled for this tenant, as the server lists them; null when
    /// the server did not say (every shipped module).
    /// </summary>
    public string[]? EnabledModules { get; set; }
}
