using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Facebook.Deployment;
using Crest.Facebook.Login.Configuration;
using Crest.Facebook.Login.Drivers;
using Crest.Facebook.Login.Recipes;
using Crest.Facebook.Login.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;

namespace Crest.Facebook;

[Feature(FacebookConstants.Features.Login)]
public sealed class StartupLogin : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AdminMenuLogin>();

        services.AddSingleton<IFacebookLoginService, FacebookLoginService>();
        services.AddSiteDisplayDriver<FacebookLoginSettingsDisplayDriver>();
        services.AddRecipeExecutionStep<FacebookLoginSettingsStep>();

        // Register the options initializers required by the Facebook handler.
        // Crest-specific initializers:
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, FacebookLoginConfiguration>();
        services.AddTransient<IConfigureOptions<FacebookOptions>, FacebookLoginConfiguration>();

        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<FacebookOptions>, OAuthPostConfigureOptions<FacebookOptions, FacebookHandler>>();
    }
}

[RequireFeatures("Crest.Deployment")]
public class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<FacebookLoginDeploymentSource, FacebookLoginDeploymentStep, FacebookLoginDeploymentStepDriver>();
    }
}
