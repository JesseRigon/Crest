using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grove.Data.Migration;
using Grove.DisplayManagement.Handlers;
using Grove.Https.Drivers;
using Grove.Https.Migrations;
using Grove.Https.Services;
using Grove.Https.Settings;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;

namespace Grove.Https;

public sealed class Startup : StartupBase
{
    public override async ValueTask ConfigureAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var httpsService = serviceProvider.GetRequiredService<IHttpsService>();
        var settings = await httpsService.GetSettingsAsync();
        if (settings.RequireHttps)
        {
            app.UseHttpsRedirection();
        }

        if (settings.StrictTransportSecurityMode == HttpStrictTransportSecurityMode.Enabled ||
            (settings.StrictTransportSecurityMode == HttpStrictTransportSecurityMode.FromConfiguration &&
             serviceProvider.GetRequiredService<IHostEnvironment>().IsProduction()))
        {
            app.UseHsts();
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<HttpsSettingsDisplayDriver>();
        services.AddPageSource<AdminPages>();
        services.AddSingleton<IHttpsService, HttpsService>();
        services.AddDataMigration<HttpsSettingsMigrations>();

        services.AddPermissionProvider<Permissions>();

        services.AddOptions<HttpsRedirectionOptions>()
            .Configure<IHttpsService>((options, service) =>
            {
                var settings = service.GetSettingsAsync().GetAwaiter().GetResult();
                if (settings.RequireHttpsPermanent)
                {
                    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                }

                options.HttpsPort = settings.SslPort;
            });

        services.AddHsts(options =>
        {
            options.Preload = false;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<HttpsSettings, DeploymentStartup>(S => S["Https settings"], S => S["Exports the Https settings."]);
    }
}
