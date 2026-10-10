using Crest.Components.Modules;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Core.BlazorWasm.Extensions;
using Crest.Workflows.Studio.Extensions;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.Shell.Extensions;
using Crest.Workflows.Studio.Workflows.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Crest.Workflows.Studio.Host;

/// <summary>
/// Studio's own service container, built once when the first workflow page opens (the
/// thin pages in Crest.Workflows.BlazorWasm call <see cref="BuildAsync"/> by reflection
/// after loading this assembly and its dependencies). Nothing of Studio is registered in the
/// Crest admin's container: the admin's provider falls back to this one for Studio's
/// components. The few browser services Studio needs are forwarded from the admin app.
/// </summary>
public static class StudioServices
{
    public static async Task<IServiceProvider> BuildAsync(IServiceProvider app)
    {
        var environment = app.GetRequiredService<CrestClientEnvironment>();
        var engineApi = new Uri(environment.TenantBaseAddress, WorkflowsConstants.Routes.EngineApi);

        var services = new ServiceCollection();

        // The browser, as the admin app has it.
        Forward<IJSRuntime>(services, app);
        Forward<NavigationManager>(services, app);
        Forward<ILoggerFactory>(services, app);
        Forward<ICrestAntiforgery>(services, app);
        if (app.GetService<IJSInProcessRuntime>() is not null) Forward<IJSInProcessRuntime>(services, app);
        if (app.GetService<IConfiguration>() is not null) Forward<IConfiguration>(services, app);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddOptions();

        // Studio: core, MudBlazor, the workflows module, the engine behind the Crest cookie.
        services.AddCore();
        services.AddShell(options => options.DisableAuthorization = true);
        services.AddRemoteBackend(new BackendApiConfig
        {
            ConfigureBackendOptions = options => options.Url = engineApi,
            ConfigureHttpClientBuilder = options => options.AuthenticationHandler = typeof(CrestCookieApiHandler),
        });
        services.AddWorkflowsModule();
        services.AddScoped<IAuthenticationProviderManager, CookieAuthenticationProviderManager>();

        // One scope for the browser session, like the WASM host's own.
        var scope = services.BuildServiceProvider().CreateScope();
        var provider = scope.ServiceProvider;

        // What Studio's shell does before its first page.
        await provider.GetRequiredService<IStartupTaskRunner>().RunStartupTasksAsync();
        await provider.GetRequiredService<IFeatureService>().InitializeFeaturesAsync();
        return provider;
    }

    private static void Forward<T>(IServiceCollection services, IServiceProvider app) where T : class =>
        services.AddSingleton(_ => app.GetRequiredService<T>());
}

/// <summary>
/// Studio's API clients authenticate with the tenant's Crest cookie (the engine API's
/// gate maps the workflow permissions the user holds onto the engine's grants) and send
/// Crest's antiforgery token on every unsafe request, as Crest's own client does.
/// </summary>
public sealed class CrestCookieApiHandler(ICrestAntiforgery antiforgery) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options)
        {
            var token = await antiforgery.GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Studio asks for a bearer token for its real-time connections; in the Crest admin the
/// session is the Crest cookie, so there is none to give and the browser sends the cookie.
/// </summary>
public sealed class CookieAuthenticationProviderManager : IAuthenticationProviderManager
{
    public Task<string?> GetAuthenticationTokenAsync(string? tokenName, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
