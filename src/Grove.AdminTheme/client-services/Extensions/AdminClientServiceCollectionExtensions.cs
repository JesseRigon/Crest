using Grove.AdminTheme.Api;
using Grove.AdminTheme.Display;
using Grove.AdminTheme.State;
using Grove.AdminTheme.Options;
using Grove.AdminTheme.Theme;
using Grove.Components.Primitives;
using Grove.Components.Theme;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Grove.AdminTheme;

// Phase 8: these registrations lived in wasm/Program.cs when this project was a
// standalone WASM app. As a client library under the host app's single Blazor Web App
// entry, ClientWebAssemblyHost calls this once during WASM boot.
// Everything here is the browser-side implementation set - Grove.Server registers its
// own server-side counterparts (per-request HttpClient with forwarded auth cookie,
// RoutingOptions from AdminOptions/UserOptions, etc.) for the SSR/InteractiveServer
// phases of InteractiveAuto.
public static class AdminClientServiceCollectionExtensions
{
    public static IServiceCollection AddAdminClient(this IServiceCollection services, Uri apiBaseAddress, RoutingOptions routingOptions, Uri? tenantBaseAddress = null)
    {
        services.AddScoped(sp => new AntiforgeryHandler((IJSInProcessRuntime)sp.GetRequiredService<IJSRuntime>(), sp.GetRequiredService<Grove.Components.Modules.GroveShellContext>()) { BaseAddress = apiBaseAddress });
        services.AddScoped<IAntiforgeryTokenStore>(sp => sp.GetRequiredService<AntiforgeryHandler>());
        services.AddScoped<ICultureCookieWriter>(sp => sp.GetRequiredService<AntiforgeryHandler>());
        services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<AntiforgeryHandler>();
            handler.InnerHandler = new HttpClientHandler();
            return new HttpClient(handler) { BaseAddress = apiBaseAddress };
        });
        services.AddScoped<IApi, global::Grove.AdminTheme.Api.Api>();
        services.AddScoped<AdminShellState>();
        services.AddScoped<HttpDisplayActionDispatcher>();
        services.AddScoped<Grove.DisplayManagement.Components.IDisplayActionDispatcher>(sp => sp.GetRequiredService<HttpDisplayActionDispatcher>());
        services.AddScoped(_ => routingOptions);
        services.AddScoped<ThemeEngine>();
        services.AddScoped<GroveApiLocalizer>();
        services.AddScoped<ILocalizer>(sp => sp.GetRequiredService<GroveApiLocalizer>());
        services.AddSingleton<Grove.Components.Modules.IGroveAntiforgery>(new AntiforgeryTokenSource(apiBaseAddress));
        services.AddSingleton(new Grove.Components.Modules.GroveClientEnvironment(apiBaseAddress, tenantBaseAddress ?? apiBaseAddress));

        return services;
    }

    /// <summary>
    /// Registers the components module client assemblies mark with
    /// <see cref="Grove.Components.Modules.GroveJSComponentAttribute"/> (call with the WASM
    /// host's RootComponents): JavaScript can render them from startup on.
    /// </summary>
    public static void RegisterModuleJSComponents(this Microsoft.AspNetCore.Components.Web.IJSComponentConfiguration configuration)
    {
        foreach (var type in ModuleAssemblyRegistry.Assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
        {
            if (type.GetCustomAttributes(typeof(Grove.Components.Modules.GroveJSComponentAttribute), false).FirstOrDefault() is Grove.Components.Modules.GroveJSComponentAttribute component)
            {
                configuration.RegisterForJavaScript(type, component.Identifier, component.Initializer);
            }
        }
    }

    /// <summary>Module pages loaded on demand (the manifest the host app's WASM build generates).</summary>
    /// <remarks>
    /// The loader lives in the shell runtime so every shell can use it. The manifest's module
    /// list configures this assembly's module registry, and the registry's loaded (never lazy)
    /// modules are handed back to the loader. Call before anything reads the registry.
    /// </remarks>
    public static IServiceCollection AddLazyModules(this IServiceCollection services, Grove.Shell.LazyModules modules)
    {
        ModuleAssemblyRegistry.Configure(modules.Modules);
        modules.EagerModules = ModuleAssemblyRegistry.Assemblies;
        services.AddSingleton(modules);
        services.AddSingleton<Grove.Components.Regions.IPageRegionContributorLoader>(modules);
        return services;
    }
}
