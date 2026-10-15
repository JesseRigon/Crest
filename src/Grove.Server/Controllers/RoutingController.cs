using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Grove;
using Grove.Middlewares;
using Grove.ViewModels;

namespace Grove.Controllers;

// Anonymous by design, same as ThemeController.Get: the WASM admin shell needs
// the tenant's real, configured AdminPath/LoginPath (see BlazorAdminThemeOptions,
// derived from AdminOptions.AdminUrlPrefix/UserOptions.LoginPath) before it knows
// whether the current visitor is authenticated - Login.razor and GroveAppContainer
// both build cross-shell navigation targets from this on every boot, logged in or
// not. Program.cs fetches it before WebAssemblyHostBuilder.Build() so those
// components always have a real, navigable path the instant they render.
[ApiController]
[Route("api/grove/routing")]
public sealed class RoutingController(
    IOptions<BlazorAdminThemeOptions> options,
    IOptions<Grove.Routing.MemberOptions> memberOptions,
    Grove.Routing.IEnabledModuleAssemblies enabledModules) : ControllerBase
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
