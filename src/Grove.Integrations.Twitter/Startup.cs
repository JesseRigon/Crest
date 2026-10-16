using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Connectors;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Modules;
using Grove.Security.Permissions;
using Grove.Integrations.Twitter.Connections;
using Grove.Integrations.Twitter.Services;
using Grove.Integrations.Twitter.Signin.Configuration;
using Grove.Integrations.Twitter.Signin.Services;
using Grove.Integrations.Twitter.Signin.Settings;

namespace Grove.Integrations.Twitter;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
    }
}

/// <summary>Posting to X: the X connection (with the app credentials Sign in with X shares) and its workflow activity.</summary>
[Feature(TwitterConstants.Features.Twitter)]
public sealed class TwitterStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddXConnection();
        services.AddDataMigration<TwitterSettingsMigrations>();
    }
}

[Feature(TwitterConstants.Features.Signin)]
public sealed class TwitterSigninStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITwitterSigninService, TwitterSigninService>();
        services.AddSettingsGroup<TwitterSigninSettings>(
            TwitterConstants.Features.Signin,
            "Sign In with X (Twitter)",
            Permissions.ManageTwitterSignin,
            group => group
                .InMenu("settings-authentication", entryId: "twitter-signin")
                .Describe("The X app's client id and secret are the X app credentials on the Connections screen.")
                .ReloadsTenant());

        services.AddScoped<IConnectionChangeHandler>(sp => new AppCredentialsChangeHandler(
            sp.GetRequiredService<IShellReleaseManager>(), TwitterConstants.AppCredentialsKey, TwitterConstants.ConnectionKey));

        // Register the options initializers required by the X handler.
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, TwitterOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<XAuthenticationOptions>, TwitterOptionsConfiguration>();
        services.AddTransient<IPostConfigureOptions<XAuthenticationOptions>, OAuthPostConfigureOptions<XAuthenticationOptions, XAuthenticationHandler>>();
    }
}

/// <summary>
/// Drops the OAuth 1.0a keys the X settings used to hold (consumer key and secret, access token
/// and secret): posting runs on the X connection's OAuth 2.0 grant now, and the keys do not
/// carry over to an OAuth 2.0 client.
/// </summary>
internal sealed class TwitterSettingsMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<Grove.Infrastructure.Settings.ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();

            if (site.Properties.Remove("TwitterSettings"))
            {
                scope.ServiceProvider.GetRequiredService<ILogger<TwitterSettingsMigrations>>()
                    .LogWarning("The X (Twitter) OAuth 1.0a keys were removed; connect the X account on the Connections screen.");
                await siteService.UpdateSiteSettingsAsync(site);
            }
        });

        return 1;
    }
}
