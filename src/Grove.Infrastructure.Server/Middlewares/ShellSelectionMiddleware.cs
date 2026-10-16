using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Access;
using Grove.Routing;
using Grove.Services;
using Grove.Extensions;
using Grove.Infrastructure.Shells.Admin;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;

namespace Grove.Middlewares;

/// <summary>
/// The admin shell's paths: the admin prefix, the login path and the logout path, as the
/// tenant configured them. The source of the admin entries in the shell registry
/// (<see cref="ShellRegistryConfiguration"/>) and of the paths the client is sent.
/// </summary>
public sealed class BlazorAdminThemeOptions
{
    // Defaults match AdminOptions.AdminUrlPrefix / UserOptions.LoginPath's own stock
    // defaults ("Admin" / "Login") - BlazorAdminThemeOptionsConfiguration below
    // overrides these from the tenant's real, configured values (recipe/appsettings,
    // "Grove_Admin"/"Grove_Users" shell config sections) so a tenant that
    // customizes either path doesn't silently break admin-theme routing. These
    // property defaults only apply if that PostConfigure step is somehow skipped.
    public string AdminPath { get; set; } = "/admin";
    public string LoginPath { get; set; } = "/login";
}

// Keeps BlazorAdminThemeOptions.AdminPath/LoginPath in sync with Grove's own,
// real, tenant-configurable settings (AdminOptions.AdminUrlPrefix, UserOptions.LoginPath -
// both bound from shell config, e.g. a recipe's
// "Grove_Admin"/"Grove_Users" sections) instead of Grove hardcoding its
// own copies that silently drift if a tenant customizes any of them. Runs as
// IPostConfigureOptions so it applies after BlazorAdminThemeOptions' own
// IConfigureOptions, regardless of registration order.
internal sealed class BlazorAdminThemeOptionsConfiguration(
    IOptions<AdminOptions> adminOptions,
    IOptions<Grove.Access.Users.UserOptions> userOptions) : IPostConfigureOptions<BlazorAdminThemeOptions>
{
    public void PostConfigure(string? name, BlazorAdminThemeOptions options)
    {
        options.AdminPath = "/" + adminOptions.Value.AdminUrlPrefix;
        options.LoginPath = "/" + userOptions.Value.LoginPath;
    }
}

// Inserts ShellSelectionMiddleware ahead of the tenant pipeline's UseRouting() - Grove
// applies IStartupFilters before it adds routing (ShellPipelineExtensions), while module
// Configure() middlewares all land after, where a Request.Path rewrite can no longer
// influence which endpoint was matched. See the registration comment in
// Startup.ConfigureServices.
internal sealed class ShellSelectionStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<ShellSelectionMiddleware>();
            next(app);
        };
}

/// <summary>
/// Serves the registered shells (<see cref="IShellSelector"/>): picks the shell entry a
/// request belongs to, gates it, and shifts its base into <c>PathBase</c> so endpoint routing
/// matches the page's compile-time <c>@page</c> literal.
/// </summary>
/// <remarks>
/// <para>This middleware serves nothing itself - <c>MapRazorComponents&lt;App&gt;()</c> produces
/// every shell document and every asset flows through the static-web-assets pipeline. What
/// it does is the request gatekeeping that has to happen before endpoint routing, the same
/// way for every registered shell:</para>
/// <list type="number">
/// <item>the entry's theme check (the admin shell applies only when the tenant's admin theme is
/// a Grove Blazor theme);</item>
/// <item>canonical-casing redirect - Blazor's NavigationManager compares the browser URL against
/// <c>&lt;base href&gt;</c> ordinally, so "/login" must 302 to "/Login";</item>
/// <item>the access gate (every page request gets its caller), the entry's authentication policy
/// and, for the admin shell, per-route authorization, server-side, ahead of any rendering;</item>
/// <item>the shell-base shift that bridges the tenant-configured prefix to MapRazorComponents'
/// compile-time route table, mirroring how ModularTenantRouterMiddleware handles the tenant's
/// own RequestUrlPrefix: PathBase += shellBase, Path = the @page literal. .NET 10 has no
/// dynamic base-path support of its own (dotnet/aspnetcore#54525) - this shift IS the base-path
/// mechanism, and App.razor derives &lt;base href&gt; from the shifted PathBase.</item>
/// </list>
/// <para>Infrastructure/API requests ("{shellBase}/_framework|_content|_blazor|api/...") get a
/// Path-ONLY strip instead - see that branch for why PathBase (and therefore cookie scoping)
/// must stay at the tenant layer there.</para>
/// </remarks>
public sealed class ShellSelectionMiddleware(RequestDelegate next, ILogger<ShellSelectionMiddleware> logger)
{
    private static readonly PathString GroveAdminThemePreviewPath = new("/Grove.Themes.Admin/Theme.png");
    private const string GroveAdminThemePreviewAsset = "/_content/Grove.Themes.Admin.Client/Theme.png";

    public async Task InvokeAsync(HttpContext context)
    {
        // A background run of the tenant pipeline (ModularBackgroundService, to make the
        // synthetic context endpoint-aware) carries no request to gate: its caller is the
        // system actor the background entry point sets, never an anonymous one from here.
        if (context.Items.ContainsKey("IsBackground"))
        {
            await next(context);
            return;
        }

        var requestPath = context.Request.Path;
        // Already carries the tenant's RequestUrlPrefix (ModularTenantRouterMiddleware shifted
        // it there). Every absolute URL emitted here is composed on top of it, and the
        // shell-base shifts below append to it.
        var requestPathBase = context.Request.PathBase;

        // Grove's theme-gallery preview thumbnail, now a static web asset under _content/.
        if (requestPath.Equals(GroveAdminThemePreviewPath))
        {
            context.Response.Redirect(requestPathBase.Add(new PathString(GroveAdminThemePreviewAsset)).Value!);
            return;
        }

        var selector = context.RequestServices.GetRequiredService<IShellSelector>();
        var selection = selector.Select(context.Request);

        // blazor.web.js resolves its own infrastructure URLs against the document's
        // <base href>, so the browser asks for "/Login/_framework/dotnet.js", "/Admin/_blazor"
        // (the server-circuit hub), "/Admin/api/grove/..." etc. Those are all mapped at the
        // tenant root; strip the shell base (and, under a shell with organizations in its
        // paths, the organization slug) off Path and pass through. Deliberately a Path-ONLY
        // rewrite: cookie issuance defaults Cookie.Path to the request's PathBase, and every
        // cookie must stay scoped to the TENANT base - appending the shell base here scoped the
        // auth cookie to "/Login" once, making the session invisible to "/Admin". This runs
        // before the page gating: "/Admin/_blazor" has no file extension and would otherwise be
        // treated as a page URL, killing the interactive circuit.
        if (selection is not null && TryStripInfrastructure(selection, out var infrastructurePath))
        {
            context.Request.Path = infrastructurePath;
            try
            {
                // "{shellBase}/api/..." is an API call from that shell's document: gated like
                // every request. The shell decides the side; an X-Shell header may only
                // confirm it (docs/access.md, defect 5).
                if (infrastructurePath.StartsWithSegments("/api"))
                {
                    if (!await Gate(context).AdmitApiAsync(context, selection.Descriptor.Side))
                    {
                        return;
                    }
                }
                else if (infrastructurePath.StartsWithSegments("/_blazor"))
                {
                    // The shell's server circuit: the connection takes the shell's side, and
                    // its caller is rebuilt per operation from that side (docs/workflows.md ›
                    // Caller lifetime).
                    if (!await Gate(context).AdmitAsync(context, selection.Descriptor.Side, allowAnonymous: true))
                    {
                        return;
                    }
                }

                await next(context);
            }
            finally
            {
                context.Request.Path = requestPath;
            }

            return;
        }

        // Only page requests are gated/rewritten here. Assets (a file extension) carry no
        // caller and are served by the static-assets pipeline.
        if (selection is null || !IsPageRequest(requestPath))
        {
            // Everything the shells did not claim: the site's pages, the tenant-root API
            // surface, the workflow engine API.
            if (IsPageRequest(requestPath))
            {
                var isApi = AccessGate.IsApiPath(requestPath);
                if (isApi ? !await Gate(context).AdmitApiAsync(context, prefixSide: null) : !await Gate(context).AdmitAsync(context, CallerSide.Site, allowAnonymous: true))
                {
                    return;
                }

                if (!isApi)
                {
                    await ServeSiteAsync(context, requestPath, requestPathBase);
                    return;
                }
            }

            await next(context);
            return;
        }

        var shell = selection.Descriptor;

        // The entry's theme check. A declined request is left to the rest of the pipeline
        // (Grove's own MVC admin under a classic admin theme).
        if (shell.ThemeDetector is not null && !await IsThemeActiveAsync(context, shell, requestPath))
        {
            await next(context);
            return;
        }

        // Route matching above is case-insensitive, but Blazor's NavigationManager compares
        // the browser URL against <base href> ordinally - rendering "/login" with a <base href>
        // of "/Login/" throws "The URI ... is not contained by the base URI". Canonicalize the
        // matched base's casing with a redirect. A fixed-page entry folds every URL under it
        // onto the base itself (the statically prerendered login form's action, bookmarks).
        var canonicalPath = shell.FixedPage is not null ? shell.BasePath : shell.BasePath + selection.Remainder.Value;
        if (!string.Equals(requestPath.Value, canonicalPath, StringComparison.Ordinal))
        {
            context.Response.Redirect(requestPathBase.Add(new PathString(canonicalPath)).Value + context.Request.QueryString);
            return;
        }

        var shellBase = shell.BasePath;
        var pageRemainder = selection.Remainder;
        string? organizationId = null;
        string? organizationSlug = null;
        RouteComponentEntry? page = null;
        var isPage = false;

        if (shell.FixedPage is not null)
        {
            isPage = true;
            pageRemainder = new PathString(shell.FixedPage);
        }
        else
        {
            var table = await context.RequestServices.GetRequiredService<IRouteComponentTableManager>().GetRouteComponentTableAsync();
            if (!shell.OrganizationInPath)
            {
                (isPage, page) = Match(table, shell, pageRemainder, context.Request.Query, entry => true);
            }
            else if (Match(table, shell, pageRemainder, context.Request.Query, entry => entry.IsOrganizationFree) is (true, var free))
            {
                // A page served at the shell base, outside any organization: sign-in,
                // registration, password reset, the organization picker.
                (isPage, page) = (true, free);
            }
            else if (!pageRemainder.HasValue || pageRemainder.Value == "/")
            {
                // No organization named: the picker chooses one (and goes straight in when the
                // member has only one).
                if (shell.OrganizationPicker is not null)
                {
                    context.Response.Redirect(requestPathBase.Add(new PathString(shell.BasePath + shell.OrganizationPicker)).Value + context.Request.QueryString);
                    return;
                }
            }
            else
            {
                // "{base}/{slug}/rest": the organization is the first segment. It resolves only
                // among the signed-in user's own bindings, so it needs the user first.
                var segments = pageRemainder.Value!.TrimStart('/');
                var slash = segments.IndexOf('/');
                var slug = slash < 0 ? segments : segments[..slash];
                var rest = new PathString(slash < 0 ? "/" : segments[slash..]);

                // Authenticate on the site side first (which builds no organization caller and
                // refuses nobody): the member caller is built below, once, for the organization
                // the path names - not first for whatever the session remembered.
                if (!await Gate(context).AdmitAsync(context, CallerSide.Site, allowAnonymous: true))
                {
                    return;
                }

                if (context.User.Identity?.IsAuthenticated != true)
                {
                    if (shell.LoginPath is not null)
                    {
                        context.Response.Redirect(requestPathBase.Add(new PathString(shell.LoginPath)).Value!);
                        return;
                    }
                }
                else if (context.RequestServices.GetService<IShellOrganizationResolver>() is { } resolver
                    && await resolver.ResolveAsync(context, slug) is { Length: > 0 } resolved)
                {
                    organizationId = resolved;
                    organizationSlug = slug;
                    shellBase = shell.BasePath + "/" + slug;
                    pageRemainder = rest;
                    (isPage, page) = Match(table, shell, rest, context.Request.Query, entry => !entry.IsOrganizationFree);
                }
            }
        }

        // Every page request authenticates once, here, and gets its caller on the entry's side
        // (a member page under an organization acts in that organization). A page marked
        // [AllowAnonymous] (RouteComponentEntry.AllowsAnonymous) is public by declaration: no
        // login redirect, no route authorization - its own API calls stay authorization-checked
        // server-side like every other page's. An organization-free page acts in no
        // organization, so it takes the site side (SideFor): whoever is signed in sees it.
        // A URL naming no organization the user is bound to is the shell's not-found page, which
        // is organization-free: admitted on the site side like every organization-free page.
        var side = !isPage && shell.OrganizationInPath ? CallerSide.Site : SideFor(shell, page);
        if (!await Gate(context).AdmitAsync(context, side, allowAnonymous: true, organizationId))
        {
            // A refused page shows the shell's error page in its place, as a visitor of the
            // shell's base (the status code pages run inside this middleware, so they never see
            // this refusal). A redirect (a pending sign-in step) or an API answer stays as it is.
            if (shell.ErrorPage is null || context.Response.StatusCode != StatusCodes.Status403Forbidden || context.Response.HasStarted
                || !await Gate(context).AdmitAsync(context, CallerSide.Site, allowAnonymous: true))
            {
                return;
            }

            isPage = true;
            page = null;
            pageRemainder = ErrorPage(shell.ErrorPage, StatusCodes.Status403Forbidden);
            AnswerWith(context, StatusCodes.Status403Forbidden);
            organizationId = null;
            shellBase = shell.BasePath;
        }

        if (isPage && shell.FixedPage is null && page?.AllowsAnonymous != true)
        {
            if (shell.LoginPath is not null && context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.Redirect(requestPathBase.Add(new PathString(shell.LoginPath)).Value!);
                return;
            }

            // Admin routes map to Grove permissions (the Access route gate). A member's reach is
            // not a permission on a route - it is the organization they act in and their member
            // class, per record, in the query that reads those records, failing closed - so the
            // member shell authorizes no routes: a page being reachable says nothing about which
            // rows the member may see. The route templates are canonical ("/Features"), matched
            // against the shell-relative remainder, never the real, prefixed request path.
            if (shell.AuthorizesRoutes)
            {
                var routeAuthorization = context.RequestServices.GetRequiredService<IRouteAuthorization>();
                var caller = context.RequestServices.GetRequiredService<ICallerContextAccessor>().Current;
                if (!await routeAuthorization.CanAccessAsync(caller, pageRemainder.Value, context.RequestAborted))
                {
                    if (shell.ErrorPage is null)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }

                    // The shell's error page, in place of the page the caller may not open.
                    pageRemainder = ErrorPage(shell.ErrorPage, StatusCodes.Status403Forbidden);
                    AnswerWith(context, StatusCodes.Status403Forbidden);
                }
            }
        }

        // Bridge the tenant-configured base to MapRazorComponents' route table: PathBase +=
        // shellBase, Path = the compile-time @page literal. Endpoint routing matches the bare
        // literal, while everything PathBase-derived - NavigationManager's BaseUri, the
        // <base href> App.razor emits, redirect composition - carries tenantPrefix + shellBase.
        // A URL with no page renders the entry's not-found page inside the shell: the admin's
        // and the member's own.
        context.Items[BlazorHosting.OriginalPathItem] = requestPath.Value;
        context.Items[BlazorHosting.ShellBasePathItem] = shellBase;
        context.Items[BlazorHosting.ShellBucketItem] = shell.Shell;
        if (shell.Shell == ShellId.Admin)
        {
            // The admin marker the admin theme selector and the shell-kind accessor read.
            Grove.Infrastructure.Shells.Admin.AdminRequest.Apply(context);
        }
        context.Items[BlazorHosting.TenantBasePathItem] = requestPathBase.Value ?? string.Empty;
        if (organizationId is not null)
        {
            context.Items[BlazorHosting.OrganizationItem] = organizationId;
            context.Items[BlazorHosting.OrganizationSlugItem] = organizationSlug;
        }

        var rewrittenPath = isPage
            ? (pageRemainder.HasValue ? pageRemainder : new PathString("/"))
            : new PathString(shell.NotFoundPage);
        if (!isPage && (shell.LoginPath is null || context.User.Identity?.IsAuthenticated == true))
        {
            // The shell's not-found page answers with its status (a visitor of a shell that
            // requires sign-in is sent to its login by the page's authorization instead).
            AnswerWith(context, StatusCodes.Status404NotFound);
        }

        if (!isPage && shell.OrganizationInPath)
        {
            // An organization the user is not bound to is not found at the shell base: the
            // not-found page is organization-free.
            context.Items[BlazorHosting.ShellBasePathItem] = shellBase = shell.BasePath;
        }

        // Every cookie stays scoped to the TENANT base (see the infrastructure branch), and
        // antiforgery's is no exception: issue the token pair here, before the shift, so the
        // render reuses it rather than scoping a new cookie to the shell base.
        context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Request.PathBase = requestPathBase.Add(new PathString(shellBase));
        context.Request.Path = rewrittenPath;
        try
        {
            await next(context);
        }
        finally
        {
            context.Request.PathBase = requestPathBase;
            context.Request.Path = requestPath;
        }
    }

    // A page rendered in place of a refusal or a miss answers with that status. The status is
    // applied as the response starts: a page that renders while the response carries an error
    // status is left to the status code pages, which would render a second page into the same
    // request (as ShellResponseStatus does for the site's content page).
    private static void AnswerWith(HttpContext context, int status)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.OnStarting(() =>
        {
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        });
    }

    // The shell's error page for a status: "{ErrorPage}/{status}" (ShellDescriptor.ErrorPage).
    private static PathString ErrorPage(string errorPage, int status) => new(errorPage.TrimEnd('/') + "/" + status.ToString(CultureInfo.InvariantCulture));

    private static IAccessGate Gate(HttpContext context) => context.RequestServices.GetRequiredService<IAccessGate>();

    // The side a page request is admitted on. A member-side caller is built for an
    // organization and refused without one (never defaulted), but an organization-free page
    // of a shell with organizations in its paths - sign-in, registration, password reset, the
    // picker - is served at the shell base, outside any organization, to whoever arrives:
    // a member choosing where to act, a staff user with no binding, a visitor. It is admitted
    // on the site side, the caller a visitor with no tenant rights; the page's own API calls
    // (the sign-in, the session) declare the member side themselves and are gated as such.
    internal static CallerSide SideFor(ShellDescriptor shell, RouteComponentEntry? page) =>
        shell.OrganizationInPath && page?.IsOrganizationFree == true ? CallerSide.Site : shell.Side;

    // The site shell, the fallback with no base of its own, resolved through the route table
    // like the other shells (one routing system, ruling 2026-10-13): a module site page is
    // served at its literal; a path a legacy MVC endpoint owns (the legacy admin frame's
    // screens, the error pages, the stock account screens) passes through untouched - that is
    // the forward to the legacy shell, until MVC is gone; everything else (a designed page, a
    // content path, a miss) is the content page, "/", which reads the real path from the stash
    // (IShellRequestPath). Endpoint routing is never left to referee between a Blazor catch-all
    // and MVC or static files: the site has no catch-all page.
    private async Task ServeSiteAsync(HttpContext context, PathString requestPath, PathString requestPathBase)
    {
        var table = await context.RequestServices.GetRequiredService<IRouteComponentTableManager>().GetRouteComponentTableAsync();
        var legacy = context.RequestServices.GetService<ILegacyRouteOwnership>();
        var resolution = SiteRouteResolver.Resolve(table, requestPath, () => legacy?.Owns(context, requestPath) == true);

        if (resolution.Kind == SiteRouteKind.Legacy)
        {
            await next(context);
            return;
        }

        context.Items[BlazorHosting.OriginalPathItem] = requestPath.Value;
        context.Items[BlazorHosting.ShellBucketItem] = ShellId.Site;
        context.Items[BlazorHosting.TenantBasePathItem] = requestPathBase.Value ?? string.Empty;
        context.Request.Path = resolution.Path;
        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Path = requestPath;
        }
    }

    // "{base}/_framework|_content|_blazor|api/..." and, under a shell with organizations in its
    // paths, "{base}/{slug}/_framework|..." too: the document base there is "{base}/{slug}/".
    // The organization of an API call is its X-Org header, which the member client sets from
    // the same path; the slug is only stripped here.
    private static bool TryStripInfrastructure(ShellSelection selection, out PathString infrastructurePath)
    {
        if (IsInfrastructure(selection.Remainder))
        {
            infrastructurePath = selection.Remainder;
            return true;
        }

        if (selection.Descriptor.OrganizationInPath && selection.Remainder.Value is { Length: > 1 } value)
        {
            var slash = value.IndexOf('/', 1);
            if (slash > 0)
            {
                var rest = new PathString(value[slash..]);
                if (IsInfrastructure(rest))
                {
                    infrastructurePath = rest;
                    return true;
                }
            }
        }

        infrastructurePath = PathString.Empty;
        return false;
    }

    // The whole client-side app (WASM HttpClient, SignalR hub connections, the pre-boot
    // routing-options fetch) addresses api/grove/* relative to the document base, so under a
    // shell those arrive as "{shellBase}/api/..." - normalized here to the tenant-root api
    // surface the controllers/hubs are mapped at. Grove's own admin never routes
    // "{AdminUrlPrefix}/api/..." ("api" is not an area), so nothing legitimate is shadowed.
    private static bool IsInfrastructure(PathString remainder) =>
        remainder.StartsWithSegments("/_framework")
        || remainder.StartsWithSegments("/_content")
        || remainder.StartsWithSegments("/_blazor")
        || remainder.StartsWithSegments("/api");

    internal static bool IsPageRequest(PathString requestPath)
    {
        var value = requestPath.Value;
        return string.IsNullOrEmpty(value) || !Path.HasExtension(value);
    }

    // The remainder is the request path with the shell base already stripped - the same
    // canonical shape the route table's entries are in, since @page directives carry no
    // prefix. Matching is scoped to the entry's own shell: the same literal can exist in
    // several shells, and matching across them would let one shell's URL resolve to - or be
    // hidden by - another shell's page. (matched, entry): the entry is null for the one
    // non-table match below.
    private static (bool Matched, RouteComponentEntry? Entry) Match(
        RouteComponentTable table,
        ShellDescriptor shell,
        PathString remainder,
        IQueryCollection query,
        Func<RouteComponentEntry, bool> filter)
    {
        var normalized = NormalizeRoute(remainder.Value);
        if (shell.Shell == ShellId.Admin &&
            string.Equals(normalized, "/settings", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(query["groupId"], "SecurityHeaders", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        return table.TryMatch(new PathString(normalized), shell.Shell, filter, out var matched)
            ? (true, matched)
            : (false, null);
    }

    private static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || route == "/")
        {
            return "/";
        }

        return "/" + route.Trim('/').ToLowerInvariant();
    }

    // The entry's theme detector, asked in the tenant's request scope. Its own job here is
    // purely "how do I get a scope to ask in": this middleware can run before the tenant's
    // request scope carries the theme services (very early pipeline positions).
    private async Task<bool> IsThemeActiveAsync(HttpContext context, ShellDescriptor shell, PathString requestPath)
    {
        if (context.RequestServices.GetService<IBlazorAdminThemeDetector>() is not null)
        {
            return await shell.ThemeDetector!(context.RequestServices);
        }

        var shellHost = context.RequestServices.GetService<IShellHost>();
        if (shellHost is null)
        {
            logger.LogDebug("Shell theme check for {Path}: no shell host is available.", requestPath);
            return false;
        }

        await shellHost.InitializeAsync();

        if (!shellHost.TryGetSettings("Default", out var shellSettings))
        {
            logger.LogDebug("Shell theme check for {Path}: Default shell settings are not available.", requestPath);
            return false;
        }

        var isActive = false;
        await (await shellHost.GetScopeAsync(shellSettings)).UsingServiceScopeAsync(async scope =>
        {
            isActive = await shell.ThemeDetector!(scope.ServiceProvider);
        });

        return isActive;
    }
}
