using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.Google.Analytics;
using Crest.Google.Analytics.Drivers;
using Crest.Google.Analytics.Services;
using Crest.Google.Analytics.Settings;
using Crest.Google.Authentication.Configuration;
using Crest.Google.Authentication.Drivers;
using Crest.Google.Authentication.Services;
using Crest.Google.Authentication.Settings;
using Crest.Google.TagManager;
using Crest.Google.TagManager.Drivers;
using Crest.Google.TagManager.Services;
using Crest.Google.TagManager.Settings;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;

namespace Crest.Google;

[Feature(GoogleConstants.Features.GoogleAuthentication)]
public sealed class GoogleAuthenticationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<GoogleAuthenticationPermissionProvider>();
        services.AddSingleton<GoogleAuthenticationService, GoogleAuthenticationService>();
        services.AddSiteDisplayDriver<GoogleAuthenticationSettingsDisplayDriver>();
        services.AddNavigationProvider<GoogleAuthenticationAdminMenu>();

        // Register the options initializers required by the Google Handler.
        // Crest-specific initializers:
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

        services.AddSiteDisplayDriver<GoogleAnalyticsSettingsDisplayDriver>();
        services.AddNavigationProvider<GoogleAnalyticsAdminMenu>();

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
        services.AddSiteDisplayDriver<GoogleTagManagerSettingsDisplayDriver>();
        services.AddNavigationProvider<GoogleTagManagerAdminMenu>();

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<GoogleTagManagerFilter>();
        });
    }
}

[Feature(GoogleConstants.Features.GoogleAuthentication)]
[RequireFeatures("Crest.Deployment")]
public sealed class GoogleAuthenticationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleAuthenticationSettings, GoogleAuthenticationDeploymentStartup>(S => S["Google Authentication Settings"], S => S["Exports the Google Authentication settings."]);
    }
}

[Feature(GoogleConstants.Features.GoogleAnalytics)]
[RequireFeatures("Crest.Deployment")]
public sealed class GoogleAnalyticsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleAnalyticsSettings, GoogleAnalyticsDeploymentStartup>(S => S["Google Analytics Settings"], S => S["Exports the Google Analytics settings."]);
    }
}

[Feature(GoogleConstants.Features.GoogleTagManager)]
[RequireFeatures("Crest.Deployment")]
public sealed class GoogleTagManagerDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleTagManagerSettings, GoogleTagManagerDeploymentStartup>(S => S["Google Tag Manager Settings"], S => S["Exports the Google Tag Manager settings."]);
    }
}
