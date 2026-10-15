using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.ReverseProxy.Configuration;
using Grove.ReverseProxy.Settings;
using Grove.Security.Permissions;
using Grove.Settings;
using Grove.Entities;
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
        services.AddPermissionProvider<Permissions>();
        services.AddSettingsGroup<ForwardedHeadersModel>(
            ForwardedHeadersModel.GroupId,
            "Reverse Proxy",
            Permissions.ManageReverseProxySettings,
            group => group
                .InMenu("settings", entryId: "reverseproxy")
                .Describe("See https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer for the forwarded headers.")
                .ReloadsTenant()
                .StoredBy(
                    async (services, _) => ForwardedHeadersModel.From(await services.GetRequiredService<ISiteService>().GetSettingsAsync<ReverseProxySettings>()),
                    async (services, model, _) =>
                    {
                        var siteService = services.GetRequiredService<ISiteService>();
                        var site = await siteService.LoadSiteSettingsAsync();
                        var settings = site.GetOrCreate<ReverseProxySettings>();
                        model.ApplyTo(settings);
                        site.Put(settings);
                        await siteService.UpdateSiteSettingsAsync(site);
                    }));

        services.AddTransient<IConfigureOptions<ReverseProxySettings>, ReverseProxySettingsConfiguration>();
        services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, ForwardedHeadersOptionsConfiguration>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<ReverseProxySettings>();
    }
}
