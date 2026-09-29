using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Components.Modules;

/// <summary>
/// A module's client (its <c>blazor-wasm</c> project) registering services into the Crest
/// admin client. The admin client finds every implementation in the module assemblies it
/// was built with (the generated module registry) and calls it once at startup, after
/// Crest's own services. Most modules only contribute pages and need none; a module that
/// hosts a component library with its own services (a designer, an editor) does.
/// </summary>
public interface ICrestClientModule
{
    void ConfigureServices(IServiceCollection services, CrestClientModuleContext context);

    /// <summary>
    /// Components the module's JavaScript renders (custom elements, JS root components):
    /// registered on the WASM host's root components at startup.
    /// </summary>
    void ConfigureJSComponents(IJSComponentConfiguration configuration)
    {
    }
}

/// <summary>
/// What a client module knows about the host. <see cref="ApiBaseAddress"/> is where Crest's
/// own <c>api/crest/*</c> calls resolve (the admin document base; the server maps it to the
/// tenant). <see cref="TenantBaseAddress"/> is the tenant root (its URL prefix included),
/// for a module's own endpoints mapped in the tenant outside <c>api/crest</c>.
/// </summary>
public sealed record CrestClientModuleContext(Uri ApiBaseAddress, Uri TenantBaseAddress);

/// <summary>
/// Orchard's antiforgery request token for the signed-in session, for a module's own
/// HTTP clients: Crest APIs and module APIs behind the same cookie validate it on every
/// unsafe request. Crest's own API client adds it by itself.
/// </summary>
public interface ICrestAntiforgery
{
    Task<CrestAntiforgeryRequestToken> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed record CrestAntiforgeryRequestToken(string HeaderName, string RequestToken);
