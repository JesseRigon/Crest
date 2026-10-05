using Crest.Admin.Api;
using Crest.Admin.DisplayManagement;
using Crest.Admin.Options;
using Crest.Admin.Theme;
using Crest.Components.Primitives;
using Crest.Components.Theme;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Crest.Admin;

// Phase 8: these registrations lived in wasm/Program.cs when this project was a
// standalone WASM app. As a client library under the single Blazor Web App entry
// (Crest.Client/Program.cs), the entry calls this once during WASM boot.
// Everything here is the browser-side implementation set - Crest.Server registers its
// own server-side counterparts (per-request HttpClient with forwarded auth cookie,
// CrestRoutingOptions from AdminOptions/UserOptions, etc.) for the SSR/InteractiveServer
// phases of InteractiveAuto.
public static class CrestAdminClientServiceCollectionExtensions
{
    public static IServiceCollection AddCrestAdminClient(this IServiceCollection services, Uri apiBaseAddress, CrestRoutingOptions routingOptions, Uri? tenantBaseAddress = null)
    {
        services.AddScoped(sp => new CrestAntiforgeryHandler((IJSInProcessRuntime)sp.GetRequiredService<IJSRuntime>()) { BaseAddress = apiBaseAddress });
        services.AddScoped<ICrestAntiforgeryTokenStore>(sp => sp.GetRequiredService<CrestAntiforgeryHandler>());
        services.AddScoped<ICrestCultureCookieWriter>(sp => sp.GetRequiredService<CrestAntiforgeryHandler>());
        services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<CrestAntiforgeryHandler>();
            handler.InnerHandler = new HttpClientHandler();
            return new HttpClient(handler) { BaseAddress = apiBaseAddress };
        });
        services.AddScoped<IApi, global::Crest.Admin.Api.Api>();
        services.AddScoped<DisplayManager>();
        services.AddScoped(_ => routingOptions);
        services.AddScoped<CrestThemeEngine>();
        services.AddScoped<CrestApiLocalizer>();
        services.AddScoped<ILocalizer>(sp => sp.GetRequiredService<CrestApiLocalizer>());
        services.AddSingleton<Crest.Components.Modules.ICrestAntiforgery>(new CrestAntiforgeryTokenSource(apiBaseAddress));
        services.AddSingleton(new Crest.Components.Modules.CrestClientEnvironment(apiBaseAddress, tenantBaseAddress ?? apiBaseAddress));

        return services;
    }

    /// <summary>
    /// Registers the components module client assemblies mark with
    /// <see cref="Crest.Components.Modules.CrestJSComponentAttribute"/> (call with the WASM
    /// host's RootComponents): JavaScript can render them from startup on.
    /// </summary>
    public static void RegisterCrestModuleJSComponents(this Microsoft.AspNetCore.Components.Web.IJSComponentConfiguration configuration)
    {
        foreach (var type in CrestModuleAssemblyRegistry.Assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
        {
            if (type.GetCustomAttributes(typeof(Crest.Components.Modules.CrestJSComponentAttribute), false).FirstOrDefault() is Crest.Components.Modules.CrestJSComponentAttribute component)
            {
                configuration.RegisterForJavaScript(type, component.Identifier, component.Initializer);
            }
        }
    }

    /// <summary>Module pages loaded on demand (the manifest Crest.Client's build generates).</summary>
    /// <remarks>
    /// The loader lives in the shell runtime so every shell can use it; the build's registry
    /// of modules that were never lazy is internal to this assembly, so it is handed over here.
    /// </remarks>
    public static IServiceCollection AddCrestLazyModules(this IServiceCollection services, Crest.Shell.CrestLazyModules modules)
    {
        modules.EagerModules = CrestModuleAssemblyRegistry.Assemblies;
        services.AddSingleton(modules);
        services.AddSingleton<Crest.Components.Regions.IPageRegionContributorLoader>(modules);
        return services;
    }
}
