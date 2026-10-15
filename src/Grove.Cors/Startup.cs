using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Cors.Services;
using Grove.Cors.Settings;
using Grove.Localization;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;
using CorsService = Grove.Cors.Services.CorsService;

namespace Grove.Cors;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.Cors;

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseCors();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPageSource<AdminPages>();
        services.AddPermissionProvider<Permissions>();
        services.AddSingleton<CorsService>();
        services.AddScoped<IJSLocalizer, CorsJSLocalizer>();

        services.AddTransient<IConfigureOptions<CorsOptions>, CorsOptionsConfiguration>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<CorsSettings, DeploymentStartup>(S => S["Cors settings"], S => S["Exports the Cors settings."]);
    }
}
