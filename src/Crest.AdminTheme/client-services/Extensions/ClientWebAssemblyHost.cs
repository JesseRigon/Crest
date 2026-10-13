using System.Net.Http.Json;
using Crest.AdminTheme.Options;
using Crest.Components.Primitives;
using Crest.Icons;
using Crest.Shell;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.DependencyInjection;

using Crest.Components.Modules;

namespace Crest.AdminTheme;

/// <summary>
/// The one WASM boot path for a Crest Blazor Web App. A Blazor Web App has exactly one
/// browser payload with one Program.Main, and that entry project belongs to the HOST app:
/// it references the theme client libraries and the module client libraries (*.BlazorWasm)
/// it ships - from source or from packages - imports Crest.LazyModules, and its Program.cs is
/// <c>await ClientWebAssemblyHost.RunAsync(args, LazyModulesManifest.Create());</c>.
/// Crest never references a host's modules. This boot path lives in the admin theme's
/// SERVICES (Crest.AdminTheme.Client.Services, eager), not in any theme UI: Program.cs names no
/// theme UI assembly, and each theme UI loads when its shell's document boots.
/// </summary>
/// <remarks>
/// blazor.web.js activates the one root component (Crest.Shell.CrestShellRoot, mounted by
/// Crest.Server's Components/App.razor with @rendermode InteractiveAuto and the shell's name),
/// which loads the shell's theme UI and renders its Routes - no RootComponents.Add&lt;T&gt;()
/// here. The antiforgery-wrapped HttpClient is THE HttpClient for everything client-side
/// (the antiforgery header is only attached to unsafe requests, credentials are same-origin).
/// </remarks>
public static class ClientWebAssemblyHost
{
    public static async Task RunAsync(string[] args, Crest.Shell.LazyModules lazyModules)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Module client libraries loaded on demand attach their own service containers later
        // (Crest.Components.Modules.CrestLateServiceProviders); the host's provider falls back to them.
        builder.ConfigureContainer(new ServiceProviderFactory());

        // The document base itself (tenantPrefix + shellBase + "/", from the <base href>
        // the shell selection middleware/App.razor composed) is the API base: every api/crest/*
        // URL is issued relative to it and the server middleware strips the shell base back
        // off ("/t2/Admin/api/..." -> tenant-root "/api/..."). The client therefore needs no
        // origin-root or tenant-prefix knowledge at all - an authority-root base would
        // break URL-prefixed tenants by escaping the tenant prefix entirely.
        var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        var apiBaseAddress = appBaseAddress;

        // Fetched anonymously, before Build(), the same way builder.HostEnvironment itself
        // resolves appsettings.json - Login.razor and CrestAppContainer both need the
        // tenant's real, configured AdminPath/LoginPath (see RoutingController) to
        // build cross-shell navigation targets the instant they render, before any
        // authenticated API call could complete. Under InteractiveAuto this does not block
        // first paint (SSR and the server circuit cover it while WASM boots).
        var routingOptions = await FetchRoutingOptionsAsync(apiBaseAddress);

        // Cross-shell navigation targets must be browser-absolute: the server sends the
        // tenant base with the routing options (the client never derives it from the page
        // base) and every shell path is composed on it. Server-side DI mirrors this from
        // Request.PathBase (see Startup.cs's RoutingOptions factory).
        var tenantBase = routingOptions.TenantBase;
        // The shell this document was served by, from the base path: it is what every API
        // call declares in its X-Shell header, so the server builds the caller for that side
        // and never defaults one.
        var shell = ShellOf(appBaseAddress.AbsolutePath, routingOptions);
        // A member document under an organization ("/members/{slug}/") names it by slug; the
        // member shell resolves the slug to the organization its API calls declare (X-Org).
        builder.Services.AddSingleton(new CrestShellContext
        {
            Shell = shell,
            TenantBase = tenantBase,
            OrganizationSlug = OrganizationSlugOf(appBaseAddress.AbsolutePath, routingOptions),
        });
        // A disabled module is absent: the lazy loader fetches only the modules the server
        // lists as enabled for this tenant.
        if (routingOptions.EnabledModules is { } enabledModules)
        {
            lazyModules.Restrict(enabledModules);
        }

        routingOptions.AdminPath = tenantBase + routingOptions.AdminPath;
        routingOptions.LoginPath = tenantBase + routingOptions.LoginPath;
        routingOptions.MemberPath = tenantBase + routingOptions.MemberPath;

        builder.Services.AddAdminClient(apiBaseAddress, routingOptions, new Uri(appBaseAddress, tenantBase + "/"));
        builder.Services.AddLazyModules(lazyModules);
        // The component registry of this document's shell (docs/blazor-display.md § 1), what
        // DisplayTreeRenderer resolves nodes through: the shared libraries, then the shell's
        // theme UI and module libraries as they load.
        builder.Services.AddShellComponentRegistry(shell, ShellComponentAssemblies(shell));
        builder.RootComponents.RegisterModuleJSComponents();
        builder.Services.AddIconClient();
        builder.Services.AddCrestComponents();

        await builder.Build().RunAsync();
    }

    // The document base is tenantBase + shellBase + "/": the shell is whichever configured
    // shell path follows the tenant base, and the site when none does.
    internal static string ShellOf(string documentBasePath, RoutingOptions routingOptions)
    {
        var shellBase = ShellBaseOf(documentBasePath, routingOptions);

        return shellBase switch
        {
            _ when shellBase.Equals(routingOptions.AdminPath, StringComparison.OrdinalIgnoreCase) => CrestShellContext.Admin,
            _ when shellBase.Equals(routingOptions.LoginPath, StringComparison.OrdinalIgnoreCase) => CrestShellContext.Admin,
            _ when shellBase.Equals(routingOptions.MemberPath, StringComparison.OrdinalIgnoreCase) => CrestShellContext.Member,
            // A member document under an organization: "/members/{slug}".
            _ when OrganizationSlugOf(documentBasePath, routingOptions) is not null => CrestShellContext.Member,
            _ => CrestShellContext.Site,
        };
    }

    /// <summary>
    /// The organization slug of a member document base ("/members/{slug}/"), or null for any
    /// other document (the member shell's own base, organization-free pages, other shells).
    /// </summary>
    internal static string? OrganizationSlugOf(string documentBasePath, RoutingOptions routingOptions)
    {
        var shellBase = ShellBaseOf(documentBasePath, routingOptions);
        var memberPath = routingOptions.MemberPath.TrimEnd('/');
        if (!shellBase.StartsWith(memberPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = shellBase[(memberPath.Length + 1)..];
        return slug.Length > 0 && !slug.Contains('/') ? slug : null;
    }

    private static string ShellBaseOf(string documentBasePath, RoutingOptions routingOptions)
    {
        var basePath = documentBasePath.TrimEnd('/');
        return basePath.StartsWith(routingOptions.TenantBase, StringComparison.OrdinalIgnoreCase)
            ? basePath[routingOptions.TenantBase.Length..]
            : basePath;
    }

    // The shared component libraries. The shell's theme UI is lazy - it loads when its shell's
    // document boots (Crest.Shell.CrestShellRoot) - and joins the registry then, as a loaded
    // theme (LazyModules.LoadedThemes), the way module libraries do.
    private static IEnumerable<System.Reflection.Assembly> ShellComponentAssemblies(string shell)
    {
        yield return typeof(CrestComponent).Assembly;
        yield return typeof(IIconRegistry).Assembly;
    }

    private static async Task<RoutingOptions> FetchRoutingOptionsAsync(Uri apiBaseAddress)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = apiBaseAddress };
            // Anonymous for real: the browser would send the session cookie, and a cookie call
            // must name its shell (X-Shell), which is what this answer is needed to work out.
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/crest/routing");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Omit);
            using var reply = await client.SendAsync(request);
            reply.EnsureSuccessStatusCode();
            var response = await reply.Content.ReadFromJsonAsync<RoutingResponse>();
            if (response is not null)
            {
                return new RoutingOptions
                {
                    TenantBase = response.TenantBase ?? string.Empty,
                    AdminPath = response.AdminPath,
                    LoginPath = response.LoginPath,
                    MemberPath = response.MemberPath,
                    EnabledModules = response.EnabledModules,
                };
            }
        }
        catch (HttpRequestException)
        {
        }

        return new RoutingOptions();
    }

    private sealed record RoutingResponse(string? TenantBase, string AdminPath, string LoginPath, string MemberPath, string[]? EnabledModules);
}
