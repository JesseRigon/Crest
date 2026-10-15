using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.ReverseProxy.Configuration;
using Grove.ReverseProxy.Drivers;
using Grove.ReverseProxy.Settings;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;

namespace Grove.ReverseProxy;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.ReverseProxy;

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseForwardedHeaders();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPageSource<AdminPages>();
        services.AddPermissionProvider<Permissions>();
        services.AddSiteDisplayDriver<ReverseProxySettingsDisplayDriver>();

        services.AddTransient<IConfigureOptions<ReverseProxySettings>, ReverseProxySettingsConfiguration>();
        services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, ForwardedHeadersOptionsConfiguration>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReverseProxySettings, DeploymentStartup>(S => S["Reverse Proxy settings"], S => S["Exports the Reverse Proxy settings."]);
    }
}
