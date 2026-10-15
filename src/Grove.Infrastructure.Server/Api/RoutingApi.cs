using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Grove;
using Grove.Middlewares;
using Grove.ViewModels;

namespace Grove.Api;

// Anonymous by design, same as ThemeController.Get: the WASM admin shell needs
// the tenant's real, configured AdminPath/LoginPath (see BlazorAdminThemeOptions,
// derived from AdminOptions.AdminUrlPrefix/UserOptions.LoginPath) before it knows
// whether the current visitor is authenticated - Login.razor and GroveAppContainer
// both build cross-shell navigation targets from this on every boot, logged in or
// not. Program.cs fetches it before WebAssemblyHostBuilder.Build() so those
// components always have a real, navigable path the instant they render.
/// <summary>The host's <c>api/grove/routing</c> endpoints.</summary>
public sealed class RoutingApi : ApiContributor
{
    public override string Name => "Routing";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("routing");
        routes.MapGet("", ApiHandlers.Handle<RoutingHandlers>(h => h.Get()));
    }
}

public sealed class RoutingHandlers(
    IOptions<BlazorAdminThemeOptions> options,
    IOptions<Grove.Routing.MemberOptions> memberOptions,
    Grove.Routing.IEnabledModuleAssemblies enabledModules) : ApiHandlers
{
    public async Task<IResult> Get()
        // "{shellBase}/api/..." is rewritten Path-only, so PathBase is still the tenant layer
        // (URL prefix, plus any host virtual directory): exactly the tenant base the client needs.
        // The enabled module list is not a secret worth an authentication round trip: the
        // public site's own modules are among them, and every page of a disabled module is
        // refused server-side whatever the browser loads.
        => Ok(new RoutingResponse(
            Request.PathBase.Value?.TrimEnd('/') ?? string.Empty,
            options.Value.AdminPath,
            options.Value.LoginPath,
            "/" + memberOptions.Value.MemberUrlPrefix,
            [.. (await enabledModules.GetEnabledAsync()).Order(StringComparer.Ordinal)]));
}
