using Crest.Indexing;
using Crest.Models;
using YesSql.Indexes;
using Crest.ViewModels;
using Crest.Components.Primitives;
using Crest.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Crest.Iconify;
using Crest.Icons;
using Crest.Services;
using Crest.Navigation;
using Crest.Middlewares;
using Crest.Endpoints;
using Crest.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Theming;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security;
using Crest.Security.Permissions;

namespace Crest;

[Feature("Crest")]
public sealed class Startup : StartupBase
{
    private const string CrestWebCors = "CrestWeb";

    public override int Order => -1000;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CrestWebCors, policy => policy
                .WithOrigins("http://localhost:5011", "http://127.0.0.1:5011")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        services.AddHttpContextAccessor();

        // ShellSelectionMiddleware rewrites Request.Path (tenant-configured admin
        // prefix -> compile-time @page literals, plus base-href-relative _framework/
        // _content/_blazor infrastructure URLs -> site root) - so it MUST run before
        // endpoint routing matches a path. Crest's tenant pipeline calls
        // UseRouting() ahead of every module middleware regardless of ConfigureOrder
        // (ShellPipelineExtensions.ConfigurePipelineAsync), which is why this is an
        // IStartupFilter (applied before UseRouting, see BuildPipelineInternalAsync)
        // and not an app.UseMiddleware call in Configure() - registered there, its
        // rewrites happen after the endpoint is already selected and change nothing.
        services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, ShellSelectionStartupFilter>();
        services.AddSignalR();
        services.AddScoped<IRequestAccess, RequestAccess>();
        // The API endpoint registry (docs/MVC/api.md › The seam): the declared layer is mapped in
        // ConfigureAsync, the defined layer is this tenant's own dynamic data source.
        services.AddSingleton<Crest.Api.DefinedApiEndpointDataSource>();
        services.AddSingleton<Crest.Api.IDefinedApiInvalidator>(sp => sp.GetRequiredService<Crest.Api.DefinedApiEndpointDataSource>());
        services.AddSingleton<Crest.Api.ApiEndpointRegistry>();
        services.AddApiContributor<Crest.Api.DisplayApi>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add<Crest.Api.ApiExceptionMvcFilter>());
        services.AddSingleton<Crest.Api.IApiEndpointRegistry>(sp => sp.GetRequiredService<Crest.Api.ApiEndpointRegistry>());
        services.AddHealthChecks().AddCheck<Crest.Api.ApiRegistryHealthCheck>(Crest.Api.ApiRegistryHealthCheck.Name);
        services.AddTransient<IConfigureOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>, Crest.Api.ApiJsonOptionsConfiguration>();
        services.AddApiValidation();
        services.AddScoped<IAdminMenuLayoutInvalidator, AdminMenuLayoutInvalidator>();
        services.AddScoped<IThemeSelector, LegacyFrameThemeSelector>();
        services.AddScoped<AdminMenuLayoutService>();
        services.AddScoped<UserMenuPreferencesService>();
        services.AddScoped<AdminMenuBuilder>();
        // The menu's captions in the Translations editor ("Admin Menus:Primary Navigation").
        services.AddScoped<Crest.Localization.Data.ILocalizationDataProvider, PageMenuLocalizationDataProvider>();
        // Caption resolution for the sidebar and app manifest: walks parent and sibling
        // translation contexts, then the shipped PO catalogs, before falling back to the
        // invariant literal - see the resolver's remarks.
        services.AddScoped<MenuCaptionResolver>();
        services.AddScoped<MenuPlacementService>();
        // Gap-free record numbers (one counter per named sequence per tenant).
        services.AddScoped<IRecordNumbering, RecordNumbering>();
        services.AddScoped<ProfileMenuService>();
        services.AddScoped<PrimaryNavMenuSettingsStore>();
        services.AddScoped<AdminSettingsNormalizer>();
        services.AddPageSource<AdminMenuGroups>();
        services.AddDynamicPageSource<ContentTypePages>();
        // Content groups: module-declared sets of content sources (types, option
        // lists, ...) the admin groups by; tenant reshaping lives in a document.
        services.AddScoped<IIconProvider, IconifyIconProvider>();
        services.AddScoped<IIconProviderSettingsStore, IconProviderSettingsStore>();
        services.AddSingleton<IIconifyLocalMirrorPathProvider, IconifyLocalMirrorPathProvider>();
        services.AddSingleton<IIconifyGitRunner, IconifyGitProcessRunner>();
        services.AddSingleton<IIconifyLocalMirrorStore, IconifyLocalMirrorStore>();
        services.AddSingleton<Crest.BackgroundTasks.IBackgroundTask, IconifyCacheBackgroundTask>();
        services.AddSingleton<SvgIconSanitizer>();
        services.AddHttpClient("Crest.Icons.Iconify");
        services.AddScoped<IIconRegistry, CompositeIconRegistry>();
        services.AddScoped<IconSourceStore>();
        services.AddScoped<IconController>();
        services.AddRecipeExecutionStep<Recipes.AdminMenuLayoutStep>();
        services.Configure<BlazorAdminThemeOptions>(options => { });
        services.AddTransient<IPostConfigureOptions<BlazorAdminThemeOptions>, BlazorAdminThemeOptionsConfiguration>();
        // Where the member shell lives, bound from the tenant's Crest_Member shell
        // configuration section (MemberOptionsConfiguration) rather than hardcoded.
        services.AddTransient<IConfigureOptions<Crest.Routing.MemberOptions>, Crest.Routing.MemberOptionsConfiguration>();
        services.AddScoped<Crest.Routing.IBlazorAdminThemeDetector, Crest.Routing.BlazorAdminThemeDetector>();
        // The registry of shells (docs/shells-and-themes.md › A registry of shells): Crest's
        // admin, login and member entries from the tenant's configured paths; a host adds its
        // own with services.Configure<ShellRegistryOptions>. The selector matches a request
        // against them; ShellSelectionMiddleware serves whichever it chose.
        services.AddTransient<IConfigureOptions<Crest.Routing.ShellRegistryOptions>, Crest.Routing.ShellRegistryConfiguration>();
        services.AddSingleton<Crest.Routing.IShellSelector, Crest.Routing.PathPrefixShellSelector>();
        // A disabled module is absent for the tenant (route table, endpoint gate, browser
        // manifest); the answer is cached for the shell's lifetime.
        services.AddSingleton<Crest.Routing.EnabledModuleAssembliesCache>();
        services.AddScoped<Crest.Routing.IEnabledModuleAssemblies, Crest.Routing.EnabledModuleAssemblies>();

        // The page registry (docs/admin-menu.md › The page registry): every page's routes, shell,
        // permissions and menu placement, declared once. The route table, the route gate and the
        // admin menu are views over it. Its declared layer is cached for the shell's lifetime
        // (a feature or theme change rebuilds the shell); designed pages are dynamic entries.
        services.AddSingleton<Crest.Routing.PageRegistryCache>();
        services.AddScoped<Crest.Routing.IPageRegistry, Crest.Routing.PageRegistry>();
        services.AddScoped<Crest.Routing.IRouteComponentTableManager, Crest.Routing.DefaultRouteComponentTableManager>();
        services.AddScoped<Crest.Routing.IDynamicPageSource, Crest.Routing.DesignedPageSource>();

        // The gate itself - see Crest.Routing.RouteGateMatcherPolicy's own comments for
        // why this is a MatcherPolicy/IEndpointSelectorPolicy and not a
        // DynamicRouteValueTransformer or EndpointDataSource filter. Registered as
        // Microsoft.AspNetCore.Routing.Matching.MatcherPolicy, the base type ASP.NET
        // Core's endpoint selection pipeline discovers policies by (it enumerates every
        // registered MatcherPolicy, not just IEndpointSelectorPolicy directly).
        services.AddSingleton<Microsoft.AspNetCore.Routing.MatcherPolicy, Crest.Routing.RouteGateMatcherPolicy>();

        // Crest.Server is the single Blazor Web App host for both API and SSR - the only
        // server needed for any Blazor-capable Crest theme (Site, Admin once Phase 8
        // converts it, or a future third-party theme). Registered unconditionally here,
        // exactly like ShellSelectionMiddleware below: whether a tenant's *currently
        // selected* theme actually uses this hosting is a per-request runtime check
        // (mirroring IsBlazorAdminThemeAsync), not a [Feature] gate - a tenant could
        // have a Blazor theme installed but be running a different one, and that
        // decision can change without a feature enable/disable.
        //
        // Both AddInteractiveServerComponents() and AddInteractiveWebAssemblyComponents()
        // are required for InteractiveAuto (Server-first on the initial visit while the
        // WASM runtime downloads in the background, then WASM for every visit after -
        // "WASM as the primary route, Server as the fallback." Static-SSR-only content
        // (the overwhelming majority - anything rendered via
        // a component binding's HtmlRenderer path) never touches
        // either of these; they only matter for components explicitly marked
        // @rendermode, i.e. genuine interactive islands like Components/Pages/
        // BlazorCounter.razor.
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        // InteractiveAuto's first render runs server-side (SSR, then a SignalR
        // circuit) before a WASM runtime is even downloaded - every component that
        // calls a relative api/crest/* endpoint via HttpClient needs one with a real
        // BaseAddress during those server-side phases too, mirroring what the WASM
        // entry (Crest.AdminTheme.Client's ClientWebAssemblyHost) sets up client-side. Scoped +
        // IHttpContextAccessor-derived base address: correct per-request even behind a
        // reverse proxy/different host header.
        //
        // Phase 8: wrapped in ForwardedAuthHandler - Admin components make
        // *authenticated* api/crest/* calls, and server-side there's no browser to
        // attach the Crest auth cookie, so the handler forwards the incoming
        // request's own Cookie header (auth + antiforgery + culture cookies) and
        // fetches the antiforgery request token through it, exactly like the WASM
        // AntiforgeryHandler does browser-side. Anonymous callers (Site's
        // BlazorCounter) are unaffected - forwarding an empty cookie set is a no-op.
        // Named handler registration (not AddHttpMessageHandler) so ForwardedAuthHandler
        // stays wired up per-request-scope explicitly below, instead of through
        // IHttpClientFactory's own handler pipeline - that pipeline resolves message
        // handlers from a rotating *internal* DI scope with its own lifetime (2 minutes
        // by default), not the calling request's scope, so a scoped handler registered
        // via AddHttpMessageHandler can get reused across unrelated requests within that
        // window (wrong user's forwarded cookies attached to someone else's request).
        // What AddHttpClient still buys us: a shared, pooled SocketsHttpHandler as the
        // *primary* handler, created once and reused - the actual fix for the socket
        // exhaustion (constructing `new HttpClientHandler()` per scope, as this used to,
        // opened a fresh unreused connection pool on every single request and eventually
        // exhausted ephemeral ports/file descriptors under load - observed as
        // SocketException "Resource temporarily unavailable" during a fresh-tenant
        // recipe run). ForwardedAuthHandler wraps that shared primary handler
        // fresh per scope below, so its own per-request state (captured cookies, cached
        // antiforgery token) stays correctly scoped while the sockets underneath it
        // don't.
        services.AddHttpClient("Crest.Server.Loopback.Primary");
        services.AddScoped(sp =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            // The tenant's base, not Request.PathBase: ShellSelectionMiddleware shifts the shell
            // base ("/Login", "/Admin") into PathBase for endpoint routing, and the APIs live
            // under the tenant base, so a relative "api/crest/..." must not resolve under the shell.
            var tenantBase = httpContext.Items.TryGetValue(BlazorHosting.TenantBasePathItem, out var item) && item is string recorded
                ? new PathString(recorded)
                : httpContext.Request.PathBase;
            var baseAddress = new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}{tenantBase.Value?.TrimEnd('/')}/");
            var handler = sp.GetRequiredService<ForwardedAuthHandler>();
            handler.BaseAddress = baseAddress;
            handler.InnerHandler = sp.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("Crest.Server.Loopback.Primary");
            return new HttpClient(handler) { BaseAddress = baseAddress };
        });
        services.AddScoped<ForwardedAuthHandler>();
        services.AddScoped<Crest.AdminTheme.Api.IAntiforgeryTokenStore>(sp => sp.GetRequiredService<ForwardedAuthHandler>());
        services.AddScoped<Crest.AdminTheme.Api.ICultureCookieWriter, NoOpCultureCookieWriter>();
        services.AddCrestComponents();
        services.AddIconClient();

        // Phase 8: the Admin theme's client-service set, server-side. Admin pages run
        // under InteractiveAuto, so their SSR/circuit phases resolve these from THIS
        // container - any admin-page dependency missing here is the AdminMenu DI bug
        // all over again (an unresolvable constructor surfacing as a blank/broken
        // page). Counterparts of Crest.AdminTheme's AddAdminClient (WASM side):
        // IApi/AdminShellState/ThemeEngine/CrestApiLocalizer ride the
        // forwarded-cookie HttpClient above; RoutingOptions comes straight from
        // the tenant's configured options (no HTTP self-call needed server-side) via
        // BlazorAdminThemeOptions, which BlazorAdminThemeOptionsConfiguration already
        // post-configures from AdminOptions.AdminUrlPrefix/UserOptions.LoginPath.
        // Culture needs no explicit registration: CultureInfo.CurrentUICulture is
        // already resolved per-request by the platform's request localization pipeline
        // (the tenant-wide culture cookie, then Accept-Language), which CrestApiLocalizer picks up.
        services.AddScoped<Crest.AdminTheme.Api.IApi, Crest.AdminTheme.Api.Api>();
        services.AddScoped<Crest.AdminTheme.State.AdminShellState>();
        // Display trees' server actions and bound slots in a circuit: over the same HTTP path the
        // browser uses (docs/blazor-display.md § 4a), never in-process from a long-lived circuit.
        services.AddScoped<Crest.AdminTheme.Display.HttpDisplayActionDispatcher>();
        services.AddScoped<Crest.DisplayManagement.Components.IDisplayActionDispatcher>(sp => sp.GetRequiredService<Crest.AdminTheme.Display.HttpDisplayActionDispatcher>());
        services.AddScoped<Crest.AdminTheme.Theme.ThemeEngine>();
        services.AddScoped<Crest.Components.Theme.CrestApiLocalizer>();
        services.AddScoped<Crest.Components.Primitives.ILocalizer>(sp => sp.GetRequiredService<Crest.Components.Theme.CrestApiLocalizer>());
        services.AddScoped(sp =>
        {
            var themeOptions = sp.GetRequiredService<IOptions<BlazorAdminThemeOptions>>().Value;
            // Composed on the tenant base so these stay real, navigable browser URLs
            // under URL-prefixed tenants, mirroring the WASM side's own composition
            // (ClientWebAssemblyHost). During SSR of an admin page the
            // middleware has already shifted the shell base into PathBase, so the
            // pre-shift value it stashed is the tenant layer; on a circuit/hub request
            // PathBase was never shifted and is already exactly that layer.
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var tenantBase = (httpContext?.Items.TryGetValue(BlazorHosting.TenantBasePathItem, out var stashed) == true
                && stashed is string stashedBase
                    ? stashedBase
                    : httpContext?.Request.PathBase.Value ?? string.Empty).TrimEnd('/');
            var memberOptions = sp.GetRequiredService<IOptions<Crest.Routing.MemberOptions>>().Value;
            return new Crest.AdminTheme.Options.RoutingOptions
            {
                TenantBase = tenantBase,
                AdminPath = tenantBase + themeOptions.AdminPath,
                LoginPath = tenantBase + themeOptions.LoginPath,
                MemberPath = tenantBase + "/" + memberOptions.MemberUrlPrefix,
            };
        });

        // The shell this request is served by, server-side (prerender and the server circuit),
        // mirroring what ClientWebAssemblyHost registers in the browser: the shell id the
        // selector stamped, the tenant base, and on a member page under an organization the
        // organization its path named.
        services.AddScoped(sp =>
        {
            var items = sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.Items;
            var shell = items?.TryGetValue(BlazorHosting.ShellBucketItem, out var stamped) == true && stamped is Crest.Routing.ShellId shellId
                ? shellId.Value
                : Crest.Components.Modules.CrestShellContext.Site;
            return new Crest.Components.Modules.CrestShellContext
            {
                Shell = shell,
                TenantBase = (items?.TryGetValue(BlazorHosting.TenantBasePathItem, out var tenantBase) == true ? tenantBase as string : null)?.TrimEnd('/') ?? string.Empty,
                OrganizationId = items?.TryGetValue(BlazorHosting.OrganizationItem, out var organization) == true ? organization as string : null,
                OrganizationSlug = items?.TryGetValue(BlazorHosting.OrganizationSlugItem, out var slug) == true ? slug as string : null,
            };
        });

        // Phase 3: Blazor participates in Crest's own shape pipeline instead of a
        // custom request-intercepting middleware - see docs/blazor-web.md
        // ("Crest's routing does not fit stock Blazor Web App hosting"
        // finding for why. Components are placed by templates, page trees and layer rules
        // (widgets are retired). What a tree may name is the component registry
        // (docs/blazor-display.md § 1), built by the display management per tenant, theme
        // and shell from the enabled features' assemblies plus the libraries added here: the
        // shared component libraries, the theme clients and the module client libraries
        // (each limited to its shell, as its router is). The scoped IComponentRegistry is the
        // current request's shell.
        services.Configure<ComponentRegistryOptions>(options =>
        {
            options.Add(typeof(Crest.Components.Primitives.ServiceCollectionExtensions).Assembly);
            options.Add(typeof(IIconRegistry).Assembly);
            foreach (var bucket in new[] { Crest.Routing.ShellId.Admin, Crest.Routing.ShellId.Site, Crest.Routing.ShellId.Member })
            {
                foreach (var assembly in Crest.Routing.ShellAssemblies.InShell(bucket))
                {
                    options.Add(assembly, Crest.Routing.RequestShellKindAccessor.ToShellKind(bucket));
                }
            }
        });
        services.Replace(ServiceDescriptor.Scoped<IShellKindAccessor, Crest.Routing.RequestShellKindAccessor>());
        services.AddScoped<IShapeBindingResolver, BlazorComponentShapeBindingResolver>();
        // The public site on the display tree (docs/blazor-display.md rework step 3).
        services.AddScoped<Crest.Display.SitePageBuilder>();
        services.AddScoped<Crest.Templates.Services.IShippedTemplateProvider, Crest.Display.DefaultContentTemplate>();
        services.AddScoped<Crest.Templates.Services.IShippedTemplateProvider, Crest.Display.AccountEmailTemplates>();
        services.AddScoped<IDisplayTreeVersionPart, Crest.Display.SiteTreeVersion>();
        services.AddScoped<Crest.ContentManagement.Handlers.IContentHandler, Crest.Display.DisplayTreeContentHandler>();
        services.AddScoped<Crest.Display.ContentEditorTreeBuilder>();
        // The value type registry (docs/blazor-display.md, module homes): the base types; modules add theirs.
        Crest.Data.Types.ValueTypeServiceCollectionExtensions.AddValueType(services);
        services.AddScoped<Crest.Shell.IShellResponseStatus, Crest.Display.ShellResponseStatus>();
        services.AddScoped<Crest.Shell.IShellRequestPath, Crest.Display.ShellRequestPath>();
        // One routing system (ruling 2026-10-13): the shell middleware resolves site paths
        // through the route table and forwards a legacy-owned path to MVC; this answers which.
        services.AddSingleton<Crest.Routing.ILegacyRouteOwnership, Crest.Routing.LegacyRouteOwnership>();

        // Content Part Lists themselves (parts, service, management API, screen) live in the
        // Crest.ContentPartLists module - an opt-in feature. Only the provider-agnostic
        // picker infrastructure stays here.

        // OptionPickerField: the reference mechanism for option sources. Providers keep
        // the field ignorant of what it is picking (content part lists today; users, content
        // items and other entity sources later).
        services.AddContentField<Fields.OptionPickerField>();
        services.AddScoped<IOptionSourceProvider, UserOptionSourceProvider>();
        services.AddScoped<IOptionSourceProvider, ContentItemOptionSourceProvider>();
        services.AddScoped<IOptionSourceProvider, TimeZoneOptionSourceProvider>();
        services.AddScoped<IOptionSourceProvider, CultureOptionSourceProvider>();
        services.AddScoped<OptionPickerFieldKeyResolver>();
        // Parent id->key resolution behind cascading filters and field-visibility
        // conditions, and the clear-on-save enforcement for hidden fields.
        services.AddScoped<OptionParentValueResolver>();
        services.AddScoped<Crest.ContentTypes.IFieldVisibilityEnforcer, FieldVisibilityEnforcer>();

        // Per-content-type index partitioning: modules register the high-volume types
        // that get their own table, and the catch-all partition skips them.
        services.AddSingleton<Indexing.OptionIndexPartitions>();
        services.AddDataMigration<Indexing.OptionPickerFieldIndexMigrations>();
        services.AddScopedIndexProvider<Indexing.OptionPickerFieldIndexProvider>();

        // HtmlRenderer (used by component bindings, ComponentShapeRenderer, for Static SSR)
        // instantiates components via this same service provider, and component base
        // classes routinely have [Inject] IJSRuntime - without any registration, DI throws
        // before rendering even starts, for components that never call JS during initial
        // render. TryAddScoped: only wins when nothing else already registered IJSRuntime
        // (interactive Server/WASM render modes register their own real one per-circuit;
        // this must never shadow that).
        services.TryAddScoped<IJSRuntime, UnsupportedJSRuntime>();
    }

    public override async ValueTask ConfigureAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider) =>
        await Crest.Api.ApiEndpointMapper.MapApiRegistryAsync(routes, serviceProvider);

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseCors(CrestWebCors);

        routes.MapHub<AdminMenuLayoutHub>("/api/crest/admin-menu-layout");

        // Interactive islands only (wasm/Pages/BlazorCounter.razor today) - the
        // endpoint this maps is what actually understands @rendermode, unlike
        // a component binding's plain HtmlRenderer (see ComponentShapeRenderer
        // and docs/blazor-display.md). AddAdditionalAssemblies pulls in
        // whichever theme wasm/*.Client.csproj projects Crest.csproj's
        // CrestThemeWasmClientProject glob resolved to, so InteractiveWebAssembly/
        // InteractiveAuto components declared in any of them are discoverable too.
        // MapStaticAssets must precede AddInteractiveWebAssemblyRenderMode, or the
        // WASM runtime's static asset endpoints ("Mapped static asset endpoints not
        // found") never get registered.
        //
        // AppDomain.CurrentDomain.GetAssemblies() only returns assemblies already
        // loaded into the process - a ProjectReference alone doesn't guarantee that by
        // this point in Configure(), since .NET only loads a referenced assembly on
        // first actual use (JIT), and nothing has touched a *.Client type yet this
        // early in startup. Force each candidate to load via a throwaway
        // Assembly.LoadFrom on its own already-resolved location before re-querying
        // GetAssemblies(), or Routes/routable @page components living in the .Client
        // project (see docs/blazor-web.md) silently 404 - MapRazorComponents<App>()
        // built its route table before the assembly was ever loaded.
        // Two naming conventions feed the route table: theme client assemblies
        // (*.Client - Site.Client, Admin.Client, Member.Client, the host's WASM entry)
        // and module-contributed Blazor page libraries (*.BlazorWasm - whatever module
        // client libraries the host app references, the same set the Crest.LazyModules
        // generator treats as modules browser-side; keep the two conventions in sync or a
        // module's pages route in one runtime and 404 in the other).
        foreach (var clientAssemblyPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.Client.dll", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.BlazorWasm.dll", SearchOption.TopDirectoryOnly)))
        {
            System.Reflection.Assembly.LoadFrom(clientAssemblyPath);
        }

        routes.MapStaticAssets();
        // The framework boot scripts are absent from the static web assets manifest by
        // construction (see BlazorFrameworkScriptEndpoints' own comment) - mapped as
        // ordinary tenant endpoints so tenant/admin prefix stripping applies to them
        // like everything else Crest routes.
        routes.MapBlazorFrameworkScripts(
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Startup>>());
        routes.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name is { } name
                    && (name.EndsWith(".Client", StringComparison.Ordinal) || name.EndsWith(".BlazorWasm", StringComparison.Ordinal)))
                .ToArray())
            // Stamps Crest.Routing.ThemeOwnerMetadata onto each @page-attributed
            // component's own endpoint (not the root), keyed off the component's
            // declaring assembly - the same assembly-name check
            // the page registry's scan uses (ShellAssemblies), so
            // there's one source of truth for "which bucket owns this assembly's
            // routes", never a second copy. ComponentTypeMetadata (added by Blazor's own
            // RazorComponentEndpointFactory before conventions run) is what makes this
            // per-endpoint rather than a single global tag on the whole data source -
            // confirmed via decompiled Microsoft.AspNetCore.Components.Endpoints source.
            // Crest.Routing.RouteGateMatcherPolicy is the sole consumer. The metadata
            // carries a ShellId, not a raw theme id - see
            // ThemeOwnerMetadata's own comment for why a theme-id comparison can never
            // disambiguate an Admin-vs-Site route collision (both a tenant's admin theme
            // and site theme are active at once, by construction).
            .Add(endpointBuilder =>
            {
                var componentType = endpointBuilder.Metadata
                    .OfType<Microsoft.AspNetCore.Components.Endpoints.ComponentTypeMetadata>()
                    .FirstOrDefault()?.Type;
                // Must agree with the page registry's scan exactly: the table
                // decides whether a URL resolves, this metadata decides which shell the
                // matched endpoint may win in. Both read the bucket from the component's
                // assembly (ShellAssemblies), so they cannot disagree - a page in one
                // bucket here and another there would be reachable in neither.
                Crest.Routing.ShellId? bucket = componentType is null
                    ? null
                    : Crest.Routing.ShellAssemblies.GetShell(componentType.Assembly);

                if (bucket is { } value)
                {
                    endpointBuilder.Metadata.Add(new Crest.Routing.ThemeOwnerMetadata(value));
                }

                // No catch-all page exists: the site's content page is "/" only, and the shell
                // middleware forwards a legacy-owned path to MVC itself (ILegacyRouteOwnership).
            });
    }
}

[Feature("Crest.Icons.TenantMedia")]
public sealed class TenantMediaIconsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IIconProvider, TenantMediaIconProvider>();
        services.AddScoped<IPermissionProvider, Permissions.IconPermissions>();
        services.AddScoped<IPermissionProvider, Permissions.ContentPartListPermissions>();
    }
}

[Feature("Crest.DesignSystem")]
public sealed class DesignSystemStartup : StartupBase
{
}
