using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.HomeRoute.Routing;
using Grove.Modules;
using Grove.Routing;

namespace Grove.HomeRoute;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.HomeRoute;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IShellRouteValuesAddressScheme, HomeRouteValuesAddressScheme>();
    }

    // "/" is served by the site shell's content route (docs/blazor-display.md rework step 3),
    // which reads the home route itself; the address scheme still generates "/" for the home item.
}
