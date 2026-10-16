using Grove.Data.Connectors;
using Grove.Data.Migration;
using Grove.Display.Management.Document;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Integrations.Google.Connections;
using Grove.Integrations.Google.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Integrations.Google.Analytics;
using Grove.Integrations.Google.Analytics.Services;
using Grove.Integrations.Google.Analytics.Settings;
using Grove.Integrations.Google.Authentication.Configuration;
using Grove.Integrations.Google.Authentication.Services;
using Grove.Integrations.Google.Authentication.Settings;
using Grove.Integrations.Google.TagManager;
using Grove.Integrations.Google.TagManager.Services;
using Grove.Integrations.Google.TagManager.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;

namespace Grove.Integrations.Google;

/// <summary>The Google connection and the Google app credentials it shares with Google sign-in.</summary>
[Feature(GoogleConstants.Features.Google)]
public sealed class GoogleStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddGoogleConnection();
    }
}

[Feature(GoogleConstants.Features.GoogleAuthentication)]
public sealed class GoogleAuthenticationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<GoogleAuthenticationPermissionProvider>();
        services.AddSingleton<GoogleAuthenticationService, GoogleAuthenticationService>();
        services.AddSettingsGroup<GoogleAuthenticationSettings>(
            GoogleConstants.Features.GoogleAuthentication,
            "Google",
            Permissions.ManageGoogleAuthentication,
            group => group
                .InMenu("settings-authentication", entryId: "google")
                .Describe("The Google OAuth client's id and secret are the Google app credentials on the Connections screen.")
                .ReloadsTenant());
        services.AddScoped<IConnectionChangeHandler>(sp => new AppCredentialsChangeHandler(
            sp.GetRequiredService<IShellReleaseManager>(), GoogleConstants.AppCredentialsKey, GoogleConstants.ConnectionKey));
        services.AddDataMigration<GoogleAuthenticationSettingsMigrations>();

        // Register the options initializers required by the Google Handler.
        // Grove-specific initializers:
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, GoogleOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<GoogleOptions>, GoogleOptionsConfiguration>();

        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<GoogleOptions>, OAuthPostConfigureOptions<GoogleOptions, GoogleHandler>>();

        services.AddTransient<IConfigureOptions<GoogleAuthenticationSettings>, GoogleAuthenticationSettingsConfiguration>();
    }
}

[Feature(GoogleConstants.Features.GoogleAnalytics)]
public sealed class GoogleAnalyticsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<GoogleAnalyticsPermissionsProvider>();
        services.AddSingleton<IGoogleAnalyticsService, GoogleAnalyticsService>();

        services.AddSettingsGroup<GoogleAnalyticsSettings>(
            GoogleConstants.Features.GoogleAnalytics,
            "Google Analytics",
            Permissions.ManageGoogleAnalytics,
            group => group.InMenu("settings-integrations", entryId: "googleAnalytics"));

        services.AddDocumentContributor<GoogleAnalyticsDocumentContributor>();
    }
}

[Feature(GoogleConstants.Features.GoogleTagManager)]
public sealed class GoogleTagManagerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<GoogleTagManagerPermissionProvider>();
        services.AddSingleton<IGoogleTagManagerService, GoogleTagManagerService>();
        services.AddSettingsGroup<GoogleTagManagerSettings>(
            GoogleConstants.Features.GoogleTagManager,
            "Google Tag Manager",
            Permissions.ManageGoogleTagManager,
            group => group.InMenu("settings-integrations", entryId: "googleTagManager"));

        services.AddDocumentContributor<GoogleTagManagerDocumentContributor>();
    }
}

[Feature(GoogleConstants.Features.GoogleAuthentication)]
[RequireFeatures("Grove.Deployment")]
public sealed class GoogleAuthenticationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleAuthenticationSettings>();
    }
}

[Feature(GoogleConstants.Features.GoogleAnalytics)]
[RequireFeatures("Grove.Deployment")]
public sealed class GoogleAnalyticsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleAnalyticsSettings>();
    }
}

[Feature(GoogleConstants.Features.GoogleTagManager)]
[RequireFeatures("Grove.Deployment")]
public sealed class GoogleTagManagerDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleTagManagerSettings>();
    }
}

/// <summary>Moves the Google client id and secret out of the sign-in settings into the Google app credentials.</summary>
internal sealed class GoogleAuthenticationSettingsMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => AppCredentialsAccess.MoveFromSiteSettingsAsync(
            scope.ServiceProvider,
            nameof(GoogleAuthenticationSettings),
            "ClientID",
            "ClientSecret",
            GoogleConstants.Features.GoogleAuthentication,
            GoogleConstants.AppCredentialsKey,
            scope.ServiceProvider.GetRequiredService<ILogger<GoogleAuthenticationSettingsMigrations>>()));

        return 1;
    }
}
