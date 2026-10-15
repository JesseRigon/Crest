using Grove.Components.Modules;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Core.BlazorWasm.Extensions;
using Grove.Workflows.Studio.Extensions;
using Grove.Workflows.Studio.Models;
using Grove.Workflows.Studio.Shell.Extensions;
using Grove.Workflows.Studio.Workflows.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Grove.Workflows.Studio.Host;

/// <summary>
/// Studio's own service container, built once when the first workflow page opens (the
/// thin pages in Grove.Workflows.BlazorWasm call <see cref="BuildAsync"/> by reflection
/// after loading this assembly and its dependencies). Nothing of Studio is registered in the
/// Grove admin's container: the admin's provider falls back to this one for Studio's
/// components. The few browser services Studio needs are forwarded from the admin app.
/// </summary>
public static class StudioServices
{
    public static async Task<IServiceProvider> BuildAsync(IServiceProvider app)
    {
        var environment = app.GetRequiredService<GroveClientEnvironment>();
        var engineApi = new Uri(environment.TenantBaseAddress, WorkflowsConstants.Routes.EngineApi);

        var services = new ServiceCollection();

        // The browser, as the admin app has it.
        Forward<IJSRuntime>(services, app);
        Forward<NavigationManager>(services, app);
        Forward<ILoggerFactory>(services, app);
        Forward<IGroveAntiforgery>(services, app);
        // The side the admin app acts in (X-Shell), as its own client sends it.
        if (app.GetService<GroveShellContext>() is not null) Forward<GroveShellContext>(services, app);
        if (app.GetService<IJSInProcessRuntime>() is not null) Forward<IJSInProcessRuntime>(services, app);
        if (app.GetService<IConfiguration>() is not null) Forward<IConfiguration>(services, app);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddOptions();

        // Studio: core, MudBlazor, the workflows module, the engine behind the Grove cookie.
        services.AddCore();
        services.AddShell(options => options.DisableAuthorization = true);
        services.AddRemoteBackend(new BackendApiConfig
        {
            ConfigureBackendOptions = options => options.Url = engineApi,
            ConfigureHttpClientBuilder = options => options.AuthenticationHandler = typeof(CookieApiHandler),
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
/// Studio's API clients authenticate with the tenant's Grove cookie (the engine API's
/// gate maps the workflow permissions the user holds onto the engine's grants), name the
/// side they act in with <c>X-Shell</c> (the access gate answers 403 to a cookie-authenticated
/// API call without it; the admin side, as Studio runs in the admin app) and send Grove's
/// antiforgery token on every unsafe request, as Grove's own client does.
/// </summary>
public sealed class CookieApiHandler(IGroveAntiforgery antiforgery, GroveShellContext? shell = null) : DelegatingHandler
{
    public const string ShellHeader = "X-Shell";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.Headers.TryAddWithoutValidation(ShellHeader, shell?.Shell ?? GroveShellContext.Admin);
        if (shell?.OrganizationId is { Length: > 0 } organizationId)
        {
            request.Headers.TryAddWithoutValidation("X-Org", organizationId);
        }

        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options)
        {
            var token = await antiforgery.GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Studio asks for a bearer token for its real-time connections; in the Grove admin the
/// session is the Grove cookie, so there is none to give and the browser sends the cookie.
/// </summary>
public sealed class CookieAuthenticationProviderManager : IAuthenticationProviderManager
{
    public Task<string?> GetAuthenticationTokenAsync(string? tokenName, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
