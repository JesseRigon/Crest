using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.ReverseProxy.Configuration;
using Crest.ReverseProxy.Drivers;
using Crest.ReverseProxy.Settings;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;

namespace Crest.ReverseProxy;

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
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();
        services.AddSiteDisplayDriver<ReverseProxySettingsDisplayDriver>();

        services.AddTransient<IConfigureOptions<ReverseProxySettings>, ReverseProxySettingsConfiguration>();
        services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, ForwardedHeadersOptionsConfiguration>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReverseProxySettings, DeploymentStartup>(S => S["Reverse Proxy settings"], S => S["Exports the Reverse Proxy settings."]);
    }
}
