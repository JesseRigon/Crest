using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.HomeRoute.Routing;
using Crest.Modules;
using Crest.Routing;

namespace Crest.HomeRoute;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.HomeRoute;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HomeRouteTransformer>();
        services.AddSingleton<IShellRouteValuesAddressScheme, HomeRouteValuesAddressScheme>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapDynamicControllerRoute<HomeRouteTransformer>("/");
    }
}
