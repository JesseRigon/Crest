using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Integrations.Twitter.Recipes;
using Grove.Integrations.Twitter.Services;
using Grove.Integrations.Twitter.Settings;
using Grove.Integrations.Twitter.Signin.Configuration;
using Grove.Integrations.Twitter.Signin.Services;
using Grove.Integrations.Twitter.Signin.Settings;
using Polly;

namespace Grove.Integrations.Twitter;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature(TwitterConstants.Features.Twitter)]
public sealed class TwitterStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsGroup<TwitterSettings>(
            TwitterConstants.Features.Twitter,
            "X (Twitter)",
            Permissions.ManageTwitter,
            group => group.InMenu("settings-integrations", entryId: "twitter").ReloadsTenant());
        services.AddSingleton<ITwitterSettingsService, TwitterSettingsService>();

        services.AddRecipeExecutionStep<TwitterSettingsStep>();

        services.AddTransient<TwitterClientMessageHandler>();
        services.AddTransient<IConfigureOptions<TwitterSettings>, TwitterSettingsConfiguration>();

        services.AddHttpClient<TwitterClient>()
            .AddHttpMessageHandler<TwitterClientMessageHandler>()
            .AddResilienceHandler("oc-handler", builder => builder
                .AddRetry(new HttpRetryStrategyOptions
                {
                    Name = "oc-retry",
                    MaxRetryAttempts = 3,
                    OnRetry = attempt =>
                    {
                        attempt.RetryDelay.Add(TimeSpan.FromSeconds(0.5 * attempt.AttemptNumber));

                        return ValueTask.CompletedTask;
                    },
                })
            );
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
            group => group.InMenu("settings-authentication", entryId: "twitter-signin").ReloadsTenant());

        // Register the options initializers required by the Twitter Handler.
        // Grove-specific initializers:
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, TwitterOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<TwitterOptions>, TwitterOptionsConfiguration>();
        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<TwitterOptions>, TwitterPostConfigureOptions>();
    }
}
