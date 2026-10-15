using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.Google.Analytics;
using Grove.Google.Analytics.Services;
using Grove.Google.Analytics.Settings;
using Grove.Google.Authentication.Configuration;
using Grove.Google.Authentication.Services;
using Grove.Google.Authentication.Settings;
using Grove.Google.TagManager;
using Grove.Google.TagManager.Services;
using Grove.Google.TagManager.Settings;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;

namespace Grove.Google;

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
            group => group.InMenu("settings-authentication", entryId: "google").ReloadsTenant());

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

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<GoogleAnalyticsFilter>();
        });
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

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<GoogleTagManagerFilter>();
        });
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
