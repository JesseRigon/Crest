using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest;
using Crest.Middlewares;
using Crest.ViewModels;

namespace Crest.Controllers;

// Anonymous by design, same as ThemeController.Get: the WASM admin shell needs
// the tenant's real, configured AdminPath/LoginPath (see BlazorAdminThemeOptions,
// derived from AdminOptions.AdminUrlPrefix/UserOptions.LoginPath) before it knows
// whether the current visitor is authenticated - Login.razor and CrestAppContainer
// both build cross-shell navigation targets from this on every boot, logged in or
// not. Program.cs fetches it before WebAssemblyHostBuilder.Build() so those
// components always have a real, navigable path the instant they render.
[ApiController]
[Route("api/crest/routing")]
public sealed class RoutingController(
    IOptions<BlazorAdminThemeOptions> options,
    IOptions<Crest.Routing.MemberOptions> memberOptions,
    Crest.Routing.IEnabledModuleAssemblies enabledModules) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RoutingResponse>> Get()
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
