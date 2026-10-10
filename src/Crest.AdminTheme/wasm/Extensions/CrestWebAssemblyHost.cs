using System.Net.Http.Json;
using Crest.AdminTheme.Options;
using Crest.Components.Primitives;
using Crest.Icons;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Crest.AdminTheme;

/// <summary>
/// The one WASM boot path for a Crest Blazor Web App. A Blazor Web App has exactly one
/// browser payload with one Program.Main, and that entry project belongs to the HOST app:
/// it references the theme client libraries and the module client libraries (*.BlazorWasm)
/// it ships - from source or from packages - imports Crest.LazyModules, and its Program.cs is
/// <c>await CrestWebAssemblyHost.RunAsync(args, CrestLazyModulesManifest.Create());</c>.
/// Crest never references a host's modules.
/// </summary>
/// <remarks>
/// blazor.web.js activates root components (the theme Routes mounted by Crest.Server's
/// Components/App.razor with @rendermode InteractiveAuto) - no RootComponents.Add&lt;T&gt;()
/// here. The antiforgery-wrapped HttpClient is THE HttpClient for everything client-side
/// (the antiforgery header is only attached to unsafe requests, credentials are same-origin).
/// </remarks>
public static class CrestWebAssemblyHost
{
    public static async Task RunAsync(string[] args, Crest.Shell.CrestLazyModules lazyModules)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Module client libraries loaded on demand attach their own service containers later
        // (Crest.Components.Modules.CrestLateServiceProviders); the host's provider falls back to them.
        builder.ConfigureContainer(new CrestServiceProviderFactory());

        // The document base itself (tenantPrefix + shellBase + "/", from the <base href>
        // BlazorAdminThemeMiddleware/App.razor composed) is the API base: every api/crest/*
        // URL is issued relative to it and the server middleware strips the shell base back
        // off ("/t2/Admin/api/..." -> tenant-root "/api/..."). The client therefore needs no
        // origin-root or tenant-prefix knowledge at all - an authority-root base would
        // break URL-prefixed tenants by escaping the tenant prefix entirely.
        var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
        var apiBaseAddress = appBaseAddress;

        // Fetched anonymously, before Build(), the same way builder.HostEnvironment itself
        // resolves appsettings.json - Login.razor and CrestAppContainer both need the
        // tenant's real, configured AdminPath/LoginPath (see CrestRoutingController) to
        // build cross-shell navigation targets the instant they render, before any
        // authenticated API call could complete. Under InteractiveAuto this does not block
        // first paint (SSR and the server circuit cover it while WASM boots).
        var routingOptions = await FetchRoutingOptionsAsync(apiBaseAddress);

        // Cross-shell navigation targets must be browser-absolute, so compose the tenant
        // base into them: the document base's path is tenantBase + shellBase + "/", and the
        // fetched AdminPath/LoginPath tell us which shell suffix to peel off to recover
        // tenantBase ("/t2/Login/" minus "/Login" -> "/t2"). Server-side DI mirrors this
        // composition from Request.PathBase (see Startup.cs's CrestRoutingOptions factory).
        var basePath = appBaseAddress.AbsolutePath.TrimEnd('/');
        var tenantBase = basePath switch
        {
            _ when basePath.EndsWith(routingOptions.AdminPath, StringComparison.OrdinalIgnoreCase)
                => basePath[..^routingOptions.AdminPath.Length],
            _ when basePath.EndsWith(routingOptions.LoginPath, StringComparison.OrdinalIgnoreCase)
                => basePath[..^routingOptions.LoginPath.Length],
            _ => basePath,
        };
        routingOptions.AdminPath = tenantBase + routingOptions.AdminPath;
        routingOptions.LoginPath = tenantBase + routingOptions.LoginPath;

        builder.Services.AddCrestAdminClient(apiBaseAddress, routingOptions, new Uri(appBaseAddress, tenantBase + "/"));
        builder.Services.AddCrestLazyModules(lazyModules);
        builder.RootComponents.RegisterCrestModuleJSComponents();
        builder.Services.AddCrestIconClient();
        builder.Services.AddCrestComponents();

        await builder.Build().RunAsync();
    }

    private static async Task<CrestRoutingOptions> FetchRoutingOptionsAsync(Uri apiBaseAddress)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = apiBaseAddress };
            var response = await client.GetFromJsonAsync<CrestRoutingResponse>("api/crest/routing");
            if (response is not null)
            {
                return new CrestRoutingOptions { AdminPath = response.AdminPath, LoginPath = response.LoginPath };
            }
        }
        catch (HttpRequestException)
        {
        }

        return new CrestRoutingOptions();
    }

    private sealed record CrestRoutingResponse(string AdminPath, string LoginPath);
}
