using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Routing;
using Crest.Services;
using Crest.Extensions;
using Crest.Admin;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;

namespace Crest.Middlewares;

public sealed class BlazorAdminThemeOptions
{
    // Defaults match AdminOptions.AdminUrlPrefix / UserOptions.LoginPath's own stock
    // defaults ("Admin" / "Login") - BlazorAdminThemeOptionsConfiguration below
    // overrides these from the tenant's real, configured values (recipe/appsettings,
    // "Crest_Admin"/"Crest_Users" shell config sections) so a tenant that
    // customizes either path doesn't silently break admin-theme routing. These
    // property defaults only apply if that PostConfigure step is somehow skipped.
    public string AdminPath { get; set; } = "/admin";
    public string LoginPath { get; set; } = "/login";
    public string LogoutPath { get; set; } = "/users/logoff";
}

// Keeps BlazorAdminThemeOptions.AdminPath/LoginPath/LogoutPath in sync with Crest's own,
// real, tenant-configurable settings (AdminOptions.AdminUrlPrefix, UserOptions.LoginPath,
// UserOptions.LogoffPath - all bound from shell config, e.g. a recipe's
// "Crest_Admin"/"Crest_Users" sections) instead of Crest hardcoding its
// own copies that silently drift if a tenant customizes any of them. Runs as
// IPostConfigureOptions so it applies after BlazorAdminThemeOptions' own
// IConfigureOptions (currently just the no-op in Startup.cs, but this keeps the
// override deterministic regardless of registration order - see
// CrestCultureCookieOptionsConfiguration for the same pattern and its rationale).
internal sealed class BlazorAdminThemeOptionsConfiguration(
    IOptions<AdminOptions> adminOptions,
    IOptions<Crest.Users.UserOptions> userOptions) : IPostConfigureOptions<BlazorAdminThemeOptions>
{
    public void PostConfigure(string? name, BlazorAdminThemeOptions options)
    {
        options.AdminPath = "/" + adminOptions.Value.AdminUrlPrefix;
        options.LoginPath = "/" + userOptions.Value.LoginPath;
        options.LogoutPath = "/" + userOptions.Value.LogoffPath;
    }
}

// This middleware no longer serves anything itself. The old WASM-SPA model
// (hand-serving index.html with a rewritten <base href> plus every framework/theme
// asset out of the wasm project's build webroot) is retired - Crest.Server's
// MapRazorComponents<App>() endpoint is the only thing that produces admin documents
// now, and every asset flows through the static-web-assets pipeline (_content/*,
// /_framework/*, with the framework boot scripts mapped by
// BlazorFrameworkScriptEndpoints). What remains here is the request *gatekeeping*
// that has to happen before endpoint routing:
//
//   1. theme check - the Blazor admin shell only applies when the tenant's selected
//      admin theme is (or is tagged as) the Blazor one;
//   2. canonical-casing redirect - Blazor's NavigationManager compares the browser
//      URL against <base href> ordinally, so "/login" must 302 to "/Login" (composed
//      on the tenant PathBase);
//   3. authentication + per-route authorization for admin Blazor pages, server-side,
//      ahead of any rendering;
//   4. the shell-base shift that bridges Crest's tenant-configured admin prefix to
//      MapRazorComponents' compile-time route table, mirroring how
//      ModularTenantRouterMiddleware handles the tenant's own RequestUrlPrefix:
//      PathBase += shellBase, Path = the @page literal ("/Admin/Features" ->
//      PathBase "/Admin" + Path "/Features", "/Login" -> "/login", admin URLs with
//      no Crest Blazor page -> "/legacy-host", whose LegacyHost.razor renders the
//      LegacyAdminFrame the client-side Router's NotFound branch shows for the same
//      URLs). .NET 10 has no dynamic base-path support of its own
//      (dotnet/aspnetcore#54525; the .NET 11 <BasePath /> component, #66388, only
//      covers the document side) - this shift IS the base-path mechanism, and
//      App.razor derives <base href> from the shifted PathBase. The shell base and
//      the pre-shift tenant base are stashed in HttpContext.Items
//      (CrestBlazorHosting) for the App root and CrestRoutingOptions composition.
//      Infrastructure/API requests ("{shellBase}/_framework|_content|_blazor|api/...")
//      get a Path-ONLY strip instead - see the comment at that branch for why
//      PathBase (and therefore cookie scoping) must stay at the tenant layer there.
// Inserts BlazorAdminThemeMiddleware ahead of the tenant pipeline's UseRouting() -
// Crest applies IStartupFilters before it adds routing (ShellPipelineExtensions),
// while module Configure() middlewares all land after, where a Request.Path rewrite
// can no longer influence which endpoint was matched. See the registration comment in
// Startup.ConfigureServices.
internal sealed class BlazorAdminThemeStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<BlazorAdminThemeMiddleware>();
            next(app);
        };
}

public sealed class BlazorAdminThemeMiddleware
{
    private const string LegacyHostRoute = "/legacy-host";
    // Member-shell route literals. These name the @page literals the member client's own
    // components declare; they are shell-relative (the member base is in PathBase by the
    // time endpoint routing sees them), which is why they carry no prefix.
    private const string MemberLoginRoute = "/login";
    private const string MemberNotFoundRoute = "/not-found";

    private static readonly PathString CrestAdminThemePreviewPath = new("/Crest.AdminTheme/Theme.png");
    private const string CrestAdminThemePreviewAsset = "/_content/Crest.AdminTheme.Client/Theme.png";

    private readonly RequestDelegate _next;
    private readonly IOptions<BlazorAdminThemeOptions> _options;
    private readonly IOptions<Crest.Routing.MemberOptions> _memberOptions;
    private readonly ILogger<BlazorAdminThemeMiddleware> _logger;

    public BlazorAdminThemeMiddleware(
        RequestDelegate next,
        IOptions<BlazorAdminThemeOptions> options,
        IOptions<Crest.Routing.MemberOptions> memberOptions,
        ILogger<BlazorAdminThemeMiddleware> logger)
    {
        _next = next;
        _options = options;
        _memberOptions = memberOptions;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (LegacyFrameThemeSelector.IsLegacyFrameRequest(context))
        {
            await _next(context);
            return;
        }

        var requestPath = context.Request.Path;
        // Already carries the tenant's RequestUrlPrefix: ModularTenantRouterMiddleware
        // shifted it there (PathBase += prefix, Path = remainder) before this tenant
        // pipeline was even invoked. Every absolute URL this middleware emits
        // (redirects) must be composed on top of it, and the shell-base shifts below
        // append to it - mirroring exactly how Crest itself layers the tenant prefix
        // on whatever PathBase the host (IIS virtual dir, reverse proxy) already set.
        var requestPathBase = context.Request.PathBase;
        var options = _options.Value;
        var adminPath = new PathString(options.AdminPath);

        // Crest's theme-gallery preview thumbnail. The wasm project is a Razor class
        // library now, so its wwwroot (including Theme.png) is a static web asset
        // under _content/ - redirect rather than resurrecting a file-serving path here.
        if (requestPath.Equals(CrestAdminThemePreviewPath))
        {
            context.Response.Redirect(requestPathBase.Add(new PathString(CrestAdminThemePreviewAsset)).Value!);
            return;
        }

        // blazor.web.js resolves its own infrastructure URLs against the document's
        // <base href> (derived from the shifted PathBase), not the site root - so the
        // browser asks for "/Login/_framework/dotnet.js", "/Admin/_blazor" (the
        // server-circuit hub), "/Admin/api/crest/..." etc. Those are all mapped at the
        // tenant root by MapRazorComponents/MapStaticAssets/BlazorFrameworkScriptEndpoints/
        // MapHub; strip the shell base off Path and pass through. Deliberately a
        // Path-ONLY rewrite, unlike the page branch's full PathBase shift below:
        // cookie issuance (auth, antiforgery, culture) defaults Cookie.Path to the
        // request's PathBase, and every cookie must stay scoped to the TENANT base -
        // appending the shell base here scoped the auth cookie to "/Login" once,
        // making the just-logged-in session invisible to "/Admin" (an infinite
        // login redirect loop). PathBase therefore stays exactly what Crest set:
        // the tenant layer. This must run before the page gating below:
        // "/Admin/_blazor" has no file extension and would otherwise be treated as a
        // page URL and rewritten to /legacy-host, killing the interactive circuit. No
        // theme check here - these requests only follow a document this middleware
        // already theme-gated, and a stray one merely 404s at root.
        if (TryStripShellPrefixForBlazorInfrastructure(
                requestPath,
                [adminPath, new PathString(options.LoginPath), new PathString("/" + _memberOptions.Value.MemberUrlPrefix)],
                out var infrastructurePath))
        {
            context.Request.Path = infrastructurePath;
            try
            {
                await _next(context);
            }
            finally
            {
                context.Request.Path = requestPath;
            }
            return;
        }

        var isAdminRoute = requestPath.StartsWithSegments(adminPath, out var adminRemainder);
        // LoginPath is a shared auth entry point served by the Blazor shell regardless
        // of admin/front-end, matched directly against options.LoginPath rather than
        // through auto-discovered @page literals - Login.razor's "@page "/login"" is a
        // WASM-router-relative route name, not a server path, so a tenant that
        // customizes LoginPath (e.g. "/signin") would otherwise never match here and
        // would silently fall through to Crest's own (unconfigured,
        // Blazor-theme-incompatible) login flow. Subpaths under LoginPath match too:
        // the login shell serves exactly one page, but URLs like "/Login/login" reach
        // browsers anyway (the statically prerendered form's action is the middleware's
        // internal composed URL, and such URLs get bookmarked) - folding them onto the
        // canonical LoginPath via the redirect below beats a dead 404.
        var isLoginRoute = requestPath.StartsWithSegments(new PathString(options.LoginPath), StringComparison.OrdinalIgnoreCase, out _);

        // The member shell, at MemberOptions.MemberUrlPrefix ("/members" by default,
        // tenant-settable). Matched the same way the admin shell is - a path-prefix match
        // producing the canonical remainder its route table is keyed on - because that is
        // all a shell selection is. Site needs no match of its own: it is the fallback
        // bucket for every request this middleware does not claim.
        var memberPath = new PathString("/" + _memberOptions.Value.MemberUrlPrefix);
        var isMemberRoute = requestPath.StartsWithSegments(memberPath, StringComparison.OrdinalIgnoreCase, out var memberRemainder);

        // Only page requests are gated/rewritten. Asset requests (anything with a file
        // extension) are none of this middleware's business anymore - admin assets are
        // root-absolute _content/* / _framework/* URLs served by the static-assets
        // pipeline, never admin-path-prefixed.
        if ((!isAdminRoute && !isLoginRoute && !isMemberRoute) || !IsPageRequest(requestPath))
        {
            await _next(context);
            return;
        }

        // The admin theme check gates the admin and login shells only. The member shell
        // is a separate, independently-active theme (a tenant has an active admin theme
        // AND an active member theme at once), so gating it on "is the Blazor admin theme
        // active" would make the member portal unreachable on any tenant running a
        // different admin theme - and the member shell is the surface a product's own
        // users live in, which must not depend on what staff see.
        if (!isMemberRoute && !await IsBlazorAdminThemeAsync(context, requestPath))
        {
            await _next(context);
            return;
        }

        // Route matching above is deliberately case-insensitive, but Blazor's
        // NavigationManager compares the browser URL against <base href> ordinally -
        // rendering the shell for "/login" with a <base href> of "/Login/" boots the
        // runtime and then throws "The URI ... is not contained by the base URI ...",
        // leaving a dead page. Canonicalize the matched prefix's casing with a
        // redirect instead.
        var canonicalPath = isLoginRoute
            ? options.LoginPath
            : isMemberRoute
                ? memberPath.Value + memberRemainder.Value
                : options.AdminPath + adminRemainder.Value;
        if (!string.Equals(requestPath.Value, canonicalPath, StringComparison.Ordinal))
        {
            context.Response.Redirect(requestPathBase.Add(new PathString(canonicalPath)).Value + context.Request.QueryString);
            return;
        }

        // Each shell matches its own remainder against the route table, scoped to its own
        // bucket: "/members/account" must find the member-bucket page at "/account", never
        // an admin page that happens to share the literal.
        var (isAdminBlazorRoute, blazorRoute) = isAdminRoute
            ? await MatchBlazorRouteAsync(context, adminRemainder, context.Request.Query, RouteBucket.Admin)
            : (false, null);
        var (isMemberBlazorRoute, memberRoute) = isMemberRoute
            ? await MatchBlazorRouteAsync(context, memberRemainder, context.Request.Query, RouteBucket.Member)
            : (false, null);
        var isBlazorPageRoute = isLoginRoute || isAdminBlazorRoute || isMemberBlazorRoute;

        // Direct URL requests are authorized on the server. In-app navigation
        // uses the login manifest's batch as a fast UI guard, but that browser
        // state is deliberately never trusted as an authorization decision.
        // A page marked [AllowAnonymous] (RouteComponentEntry.AllowsAnonymous) is
        // public by declaration: no login redirect, no route authorization - its own
        // API calls stay authorization-checked server-side like every other page's.
        if (isBlazorPageRoute && isAdminRoute && blazorRoute?.AllowsAnonymous != true)
        {
            // Crest gates the admin shell before Crest's later authentication
            // middleware. Authenticate the same Crest application cookie here
            // before making an early route decision.
            var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (authentication.Succeeded && authentication.Principal is not null)
            {
                context.User = authentication.Principal;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.Redirect(requestPathBase.Add(new PathString(options.LoginPath)).Value!);
                return;
            }

            // CrestRoutePermissionProvider's templates are canonical ("/Features",
            // "/Themes", ... matching the WASM app's own @page directives, which
            // carry no prefix themselves), not real server paths - so matching must
            // happen against the canonical form of this request, not its real,
            // tenant-configured requestPath (e.g. "/backoffice/Features" would never
            // match "/Features" otherwise). adminRemainder is already exactly that
            // canonical form.
            var routeAuthorization = context.RequestServices.GetRequiredService<CrestRouteAuthorizationService>();
            if (!await routeAuthorization.CanAccessAsync(context.User, adminRemainder.Value))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        // The member shell authenticates the same way, and redirects unauthenticated
        // requests to the MEMBER login rather than the staff one: the two login surfaces
        // are separate by construction (each is a page in its own shell at its own base),
        // not by a runtime check on one shared page.
        //
        // No route-permission check here, deliberately. Admin routes map to Crest
        // permissions, which is what CrestRouteAuthorizationService answers. A member's
        // reach is not a permission on a route - it is which organization they are acting
        // in and what their member class allows, which is per-record and belongs in the
        // query that reads those records, failing closed. Gating the route would imply the
        // page is safe once entered, which is exactly the wrong guarantee: the member
        // theme is shared by every org-bound user in the tenant, so a page being
        // reachable says nothing about which rows that member may see.
        if (isMemberBlazorRoute && memberRoute?.AllowsAnonymous != true)
        {
            var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (authentication.Succeeded && authentication.Principal is not null)
            {
                context.User = authentication.Principal;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.Redirect(requestPathBase.Add(memberPath.Add(new PathString(MemberLoginRoute))).Value!);
                return;
            }
        }

        // Bridge the tenant-configured prefix to MapRazorComponents' route table by
        // shifting the shell base into PathBase (PathBase += shellBase, Path = the
        // compile-time @page literal) - the same move ModularTenantRouterMiddleware
        // makes for the tenant's own RequestUrlPrefix, one layer further in. Endpoint
        // routing matches the bare @page literal ("/Features", "/login",
        // "/legacy-host"), while everything PathBase-derived - NavigationManager's
        // BaseUri, the <base href> App.razor emits from it, redirect composition -
        // automatically carries tenantPrefix + shellBase. The browser URL is untouched
        // (this is a server-internal rewrite): client-side, Blazor's Router resolves
        // the original URL against that same composed <base href>, landing on the same
        // page. When .NET 11's <BasePath /> component ships
        // (dotnet/aspnetcore#66388) it derives from this exact PathBase too, so the
        // document side can adopt it without touching this middleware.
        var shellBasePath = isLoginRoute
            ? options.LoginPath
            : isMemberRoute
                ? memberPath.Value!
                : options.AdminPath;
        // The shell's own identity, not inferred from the base path: RouteGateMatcherPolicy
        // and App.razor both read this to pick a bucket and a document, and a base-path
        // string cannot answer "which of three shells" the way a presence check answered
        // "admin or not". Login is part of the admin shell - it renders the admin
        // document and its page lives in the admin bucket.
        var shellBucket = isMemberRoute ? RouteBucket.Member : RouteBucket.Admin;
        context.Items[CrestBlazorHosting.OriginalPathItem] = requestPath.Value;
        context.Items[CrestBlazorHosting.ShellBasePathItem] = shellBasePath;
        context.Items[CrestBlazorHosting.ShellBucketItem] = shellBucket;
        context.Items[CrestBlazorHosting.TenantBasePathItem] = requestPathBase.Value ?? string.Empty;
        var rewrittenPath = isLoginRoute
            ? new PathString("/login")
            : isMemberRoute
                ? (isMemberBlazorRoute
                    // A member URL with no member page is a 404 inside the member shell,
                    // not the admin legacy frame: that frame is Crest's admin UI, which
                    // a member has no business being shown.
                    ? (memberRemainder.HasValue ? memberRemainder : new PathString("/"))
                    : new PathString(MemberNotFoundRoute))
                : isBlazorPageRoute
                    ? (adminRemainder.HasValue ? adminRemainder : new PathString("/"))
                    : new PathString(LegacyHostRoute);
        // Every cookie stays scoped to the TENANT base (see the infrastructure branch above),
        // and antiforgery's is no exception: Crest leaves its Cookie.Path unset, so it
        // follows the request's PathBase - which the shift below is about to extend with the
        // shell base. A page render issuing the cookie after the shift scoped it to "/Admin"
        // or "/Login", and the browser then never sent it to tenant-root endpoints such as the
        // workflow engine API (crest-workflows/api), which refused every unsafe call as
        // "Antiforgery token missing or invalid". Issue the token pair here, before the shift;
        // antiforgery keeps it for the rest of the request, so the render reuses it.
        context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Request.PathBase = requestPathBase.Add(new PathString(shellBasePath));
        context.Request.Path = rewrittenPath;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Request.PathBase = requestPathBase;
            context.Request.Path = requestPath;
        }
    }

    // shellBases is every shell's base path (admin, login, member). A shell whose
    // infrastructure requests are not stripped here loses its interactive circuit:
    // "{shellBase}/_blazor" has no file extension, so it would fall through to the page
    // gating and be rewritten to /legacy-host.
    private static bool TryStripShellPrefixForBlazorInfrastructure(
        PathString requestPath,
        PathString[] shellBases,
        out PathString infrastructurePath)
    {
        var matchedShell = false;
        var remainder = PathString.Empty;
        foreach (var shellBase in shellBases)
        {
            if (shellBase.HasValue && requestPath.StartsWithSegments(shellBase, StringComparison.OrdinalIgnoreCase, out remainder))
            {
                matchedShell = true;
                break;
            }
        }

        if (matchedShell &&
            (remainder.StartsWithSegments("/_framework") ||
             remainder.StartsWithSegments("/_content") ||
             remainder.StartsWithSegments("/_blazor") ||
             // The whole client-side app (WASM HttpClient, SignalR hub connections,
             // the pre-boot routing-options fetch) addresses api/crest/* relative to
             // the document base, so under the admin shell those arrive as
             // "{shellBase}/api/..." - normalized here to the tenant-root api surface
             // the controllers/hubs are actually mapped at. This is what lets the
             // client stay entirely base-relative (no origin-root or tenant-prefix
             // knowledge browser-side) and still work under URL-prefixed tenants.
             // Crest's own admin never routes "{AdminUrlPrefix}/api/..." (admin
             // controller routes are "{prefix}/{area}/{controller}/...", and "api" is
             // not an area), so nothing legitimate is shadowed.
             remainder.StartsWithSegments("/api")))
        {
            infrastructurePath = remainder;
            return true;
        }

        infrastructurePath = PathString.Empty;
        return false;
    }

    private static bool IsPageRequest(PathString requestPath)
    {
        var value = requestPath.Value;
        return string.IsNullOrEmpty(value) || !Path.HasExtension(value);
    }

    // adminRemainder is requestPath with the matched, real AdminPath prefix already
    // stripped (e.g. "/backoffice/Features" -> "/Features") - the same canonical shape
    // RouteComponentTable's entries are in, since @page directives themselves carry no
    // prefix. Comparing anything here against the real, absolute request path directly
    // would never match. Route discovery itself now lives entirely in
    // Crest.Routing.AdminRouteComponentTableProvider (reflection over [Route] attributes,
    // reusing the exact same table Crest.Routing.RouteGateMatcherPolicy consults) - this
    // middleware no longer scans .razor source files itself, avoiding two independent
    // "is this an Admin route" implementations drifting out of sync.
    // (matched, entry): the entry is null for the one non-table match below.
    private static async Task<(bool Matched, RouteComponentEntry? Entry)> MatchBlazorRouteAsync(HttpContext context, PathString shellRemainder, IQueryCollection query, RouteBucket bucket)
    {
        var normalized = NormalizeRoute(shellRemainder.Value);
        if (bucket == RouteBucket.Admin &&
            string.Equals(normalized, "/settings", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(query["groupId"], "SecurityHeaders", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        var tableManager = context.RequestServices.GetRequiredService<IRouteComponentTableManager>();
        var table = await tableManager.GetRouteComponentTableAsync();
        // Scoped to the shell's own bucket: the same literal can exist in several buckets,
        // and matching across buckets would let one shell's URL resolve to - or be hidden
        // by - another shell's page.
        return table.TryMatch(new PathString(normalized), bucket, out var matched)
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

    // Delegates the actual "is Blazor the active admin theme" question to
    // Crest.Routing.IBlazorAdminThemeDetector (the single source of truth
    // RouteGateMatcherPolicy and the route-component table also consult) - this method's
    // own job is purely "how do I get a DI scope to ask that question in", since this
    // middleware alone can run before the tenant's own request scope carries
    // IAdminThemeService (e.g. very early pipeline positions / cross-tenant probing).
    private async Task<bool> IsBlazorAdminThemeAsync(HttpContext context, PathString requestPath)
    {
        var detector = context.RequestServices.GetService<Crest.Routing.IBlazorAdminThemeDetector>();
        if (detector is not null)
        {
            return await detector.IsBlazorAdminThemeActiveAsync();
        }

        var shellHost = context.RequestServices.GetService<IShellHost>();
        if (shellHost is null)
        {
            _logger.LogDebug("Blazor admin route check for {Path}: no shell host is available.", requestPath);
            return false;
        }

        await shellHost.InitializeAsync();

        if (!shellHost.TryGetSettings("Default", out var shellSettings))
        {
            _logger.LogDebug("Blazor admin route check for {Path}: Default shell settings are not available.", requestPath);
            return false;
        }

        var isBlazorAdminTheme = false;
        await (await shellHost.GetScopeAsync(shellSettings)).UsingServiceScopeAsync(async scope =>
        {
            var scopedDetector = scope.ServiceProvider.GetRequiredService<Crest.Routing.IBlazorAdminThemeDetector>();
            isBlazorAdminTheme = await scopedDetector.IsBlazorAdminThemeActiveAsync();
        });

        return isBlazorAdminTheme;
    }
}
