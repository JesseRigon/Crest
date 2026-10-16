using Grove.Data.Connectors;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Integrations.Microsoft.Connections;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;
using Grove.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Grove.Integrations.Microsoft;

/// <summary>The Microsoft Graph connection and the Microsoft app credentials it shares with the sign-in providers.</summary>
[Feature(MicrosoftAuthenticationConstants.Features.Graph)]
public sealed class GraphStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConnectionSource, MicrosoftGraphConnectionSource>());
        services.AddScoped<IConnectionChangeHandler>(sp => new AppCredentialsChangeHandler(
            sp.GetRequiredService<IShellReleaseManager>(), MicrosoftAuthenticationConstants.AppCredentialsKey, MicrosoftAuthenticationConstants.GraphConnectionKey));
        services.AddDataMigration<MicrosoftSettingsMigrations>();
    }
}

/// <summary>
/// Moves the Microsoft account app id and secret, then the Entra ID application id (when no app
/// credentials were set by the first), out of the sign-in settings into the Microsoft app credentials.
/// </summary>
internal sealed class MicrosoftSettingsMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<MicrosoftSettingsMigrations>>();

            await AppCredentialsAccess.MoveFromSiteSettingsAsync(
                services,
                nameof(MicrosoftAccountSettings),
                "AppId",
                "AppSecret",
                MicrosoftAuthenticationConstants.Features.MicrosoftAccount,
                MicrosoftAuthenticationConstants.AppCredentialsKey,
                logger);

            var existing = services.GetService<IAppCredentialsProvider>() is { } provider
                ? await provider.GetAsync(MicrosoftAuthenticationConstants.AppCredentialsKey)
                : null;

            await AppCredentialsAccess.MoveFromSiteSettingsAsync(
                services,
                nameof(AzureADSettings),
                existing is null ? "AppId" : null,
                null,
                null,
                MicrosoftAuthenticationConstants.AppCredentialsKey,
                logger);

            if (existing is not null)
            {
                // The Microsoft account app is kept; drop the Entra ID application id beside it.
                var siteService = services.GetRequiredService<Grove.Infrastructure.Settings.ISiteService>();
                var site = await siteService.LoadSiteSettingsAsync();
                if (site.Properties[nameof(AzureADSettings)] is System.Text.Json.Nodes.JsonObject entra && entra.Remove("AppId"))
                {
                    logger.LogWarning("Microsoft account and Entra ID sign-in now share one app registration (the Microsoft app credentials); the Entra ID application id was dropped.");
                    await siteService.UpdateSiteSettingsAsync(site);
                }
            }
        });

        return 1;
    }
}
