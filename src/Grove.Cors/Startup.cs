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
        services.AddPermissionProvider<Permissions>();
        services.AddSingleton<CorsService>();
        services.AddSettingsGroup<CorsSettings>(
            CorsSettings.GroupId,
            "Cross-Origin Resource Sharing",
            Permissions.ManageCorsSettings,
            group => group
                .InMenu("settings-security", entryId: "cors")
                .ReloadsTenant()
                .StoredBy(
                    (services, _) => services.GetRequiredService<CorsService>().GetSettingsAsync(),
                    (services, settings, _) => services.GetRequiredService<CorsService>().UpdateSettingsAsync(settings))
                .ValidatedBy((_, settings, errors, _) =>
                {
                    CorsSettingsRules.Validate(settings, errors);
                    return Task.CompletedTask;
                }));

        services.AddTransient<IConfigureOptions<CorsOptions>, CorsOptionsConfiguration>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<CorsSettings>();
    }
}
