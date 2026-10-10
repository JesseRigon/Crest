using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Twitter.Drivers;
using Crest.Twitter.Recipes;
using Crest.Twitter.Services;
using Crest.Twitter.Settings;
using Crest.Twitter.Signin.Configuration;
using Crest.Twitter.Signin.Drivers;
using Crest.Twitter.Signin.Services;
using Polly;

namespace Crest.Twitter;

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
        services.AddSiteDisplayDriver<TwitterSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
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
        services.AddNavigationProvider<AdminMenuSignIn>();
        services.AddSingleton<ITwitterSigninService, TwitterSigninService>();
        services.AddSiteDisplayDriver<TwitterSigninSettingsDisplayDriver>();

        // Register the options initializers required by the Twitter Handler.
        // Crest-specific initializers:
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, TwitterOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<TwitterOptions>, TwitterOptionsConfiguration>();
        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<TwitterOptions>, TwitterPostConfigureOptions>();
    }
}
