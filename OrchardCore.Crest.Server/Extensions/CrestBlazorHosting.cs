namespace Crest.Extensions;

/// <summary>
/// The contract between <see cref="BlazorAdminThemeMiddleware"/> and the theme-dispatching
/// document root (Components/App.razor). When the middleware authorizes an admin/login
/// page request and rewrites its path to the canonical (prefix-stripped) form for
/// MapRazorComponents' route table, it records the decision here so the App root knows
/// to render the Admin document (Admin Routes, admin head assets, admin base href)
/// instead of the Site one. Requests that never went through the middleware's admin
/// gate (theme not selected, non-admin paths) carry no marker and get the Site document.
/// </summary>
public static class CrestBlazorHosting
{
    /// <summary>
    /// HttpContext.Items key holding the shell base path ("/Admin", "/Login", "/members",
    /// or the tenant-configured equivalents) that served this request - becomes the
    /// shell document's &lt;base href&gt; (with a trailing slash appended).
    /// </summary>
    public const string ShellBasePathItem = "Crest.BlazorAdmin.ShellBasePath";

    /// <summary>
    /// HttpContext.Items key holding the <see cref="Crest.Routing.RouteBucket"/> of the
    /// shell that served this request, boxed.
    /// </summary>
    /// <remarks>
    /// This exists because the presence of <see cref="ShellBasePathItem"/> alone cannot
    /// identify a shell once there are more than two. It used to be enough: Admin was
    /// "the marker is set" and Site was "it is not", which is exactly two states. A
    /// third shell needs the shell's own identity, so the middleware stamps the bucket
    /// here and <c>RouteGateMatcherPolicy</c> and <c>App.razor</c> read it rather than
    /// re-deriving a shell from a base path string.
    ///
    /// Absent means Site: Site is the fallback bucket, serving every request no shell
    /// claimed, and the middleware never runs for those (a bare "/" never reaches its
    /// gating). Readers therefore treat "no marker" as Site rather than as an error.
    /// </remarks>
    public const string ShellBucketItem = "Crest.BlazorAdmin.ShellBucket";

    /// <summary>
    /// HttpContext.Items key holding the original, un-rewritten request path (e.g.
    /// "/Admin/Features" before the middleware rewrote it to "/Features").
    /// </summary>
    public const string OriginalPathItem = "Crest.BlazorAdmin.OriginalPath";

    /// <summary>
    /// HttpContext.Items key holding the request's PathBase as it stood BEFORE the
    /// middleware shifted the shell base into it - i.e. Orchard's own layer: the
    /// tenant's RequestUrlPrefix (plus any host-level base). This is the base the
    /// tenant-root API surface (api/crest/*, the SignalR hubs) lives under, which is
    /// NOT the admin shell's own base - the WASM client needs it to compose API and
    /// hub URLs that survive URL-prefixed tenants.
    /// </summary>
    public const string TenantBasePathItem = "Crest.BlazorAdmin.TenantBasePath";
}
