using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Cors.Services;
using Crest.Cors.Settings;
using Crest.Localization;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using CorsService = Crest.Cors.Services.CorsService;

namespace Crest.Cors;

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
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();
        services.AddSingleton<CorsService>();
        services.AddScoped<IJSLocalizer, CorsJSLocalizer>();

        services.AddTransient<IConfigureOptions<CorsOptions>, CorsOptionsConfiguration>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<CorsSettings, DeploymentStartup>(S => S["Cors settings"], S => S["Exports the Cors settings."]);
    }
}
