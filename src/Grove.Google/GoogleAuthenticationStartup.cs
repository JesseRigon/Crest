using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.Google.Analytics;
using Grove.Google.Analytics.Drivers;
using Grove.Google.Analytics.Services;
using Grove.Google.Analytics.Settings;
using Grove.Google.Authentication.Configuration;
using Grove.Google.Authentication.Drivers;
using Grove.Google.Authentication.Services;
using Grove.Google.Authentication.Settings;
using Grove.Google.TagManager;
using Grove.Google.TagManager.Drivers;
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
        services.AddSiteDisplayDriver<GoogleAuthenticationSettingsDisplayDriver>();
        services.AddPageSource<GoogleAuthenticationPages>();

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

        services.AddSiteDisplayDriver<GoogleAnalyticsSettingsDisplayDriver>();
        services.AddPageSource<GoogleAnalyticsPages>();

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
        services.AddPageSource<GoogleTagManagerPages>();

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
        services.AddSiteSettingsPropertyDeploymentStep<GoogleAuthenticationSettings, GoogleAuthenticationDeploymentStartup>(S => S["Google Authentication Settings"], S => S["Exports the Google Authentication settings."]);
    }
}

[Feature(GoogleConstants.Features.GoogleAnalytics)]
[RequireFeatures("Grove.Deployment")]
public sealed class GoogleAnalyticsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleAnalyticsSettings, GoogleAnalyticsDeploymentStartup>(S => S["Google Analytics Settings"], S => S["Exports the Google Analytics settings."]);
    }
}

[Feature(GoogleConstants.Features.GoogleTagManager)]
[RequireFeatures("Grove.Deployment")]
public sealed class GoogleTagManagerDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<GoogleTagManagerSettings, GoogleTagManagerDeploymentStartup>(S => S["Google Tag Manager Settings"], S => S["Exports the Google Tag Manager settings."]);
    }
}
