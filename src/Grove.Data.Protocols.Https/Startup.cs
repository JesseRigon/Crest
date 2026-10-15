using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Data.Protocols.Https.Migrations;
using Grove.Data.Protocols.Https.Services;
using Grove.Data.Protocols.Https.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;

namespace Grove.Data.Protocols.Https;

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
        services.AddSettingsGroup<HttpsSettings>(
            HttpsSettings.GroupId,
            "HTTPS",
            Permissions.ManageHttps,
            group => group
                .InMenu("settings-security", entryId: "https")
                .ReloadsTenant()
                .ValidatedBy((services, _, errors, _) =>
                {
                    // For safety, HTTPS-only settings are not changed over HTTP.
                    if (services.GetService<IHttpContextAccessor>()?.HttpContext?.Request.IsHttps != true)
                    {
                        errors.Add(nameof(HttpsSettings.RequireHttps), "For safety, changing HTTPS-only settings over HTTP has been prevented.");
                    }

                    return Task.CompletedTask;
                }));
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
        services.AddSiteSettingsPropertyDeploymentStep<HttpsSettings>();
    }
}
