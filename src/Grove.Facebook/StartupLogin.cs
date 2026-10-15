using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Facebook.Deployment;
using Grove.Facebook.Login.Configuration;
using Grove.Facebook.Login.Drivers;
using Grove.Facebook.Login.Recipes;
using Grove.Facebook.Login.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;

namespace Grove.Facebook;

[Feature(FacebookConstants.Features.Login)]
public sealed class StartupLogin : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPageSource<FacebookLoginPages>();

        services.AddSingleton<IFacebookLoginService, FacebookLoginService>();
        services.AddSiteDisplayDriver<FacebookLoginSettingsDisplayDriver>();
        services.AddRecipeExecutionStep<FacebookLoginSettingsStep>();

        // Register the options initializers required by the Facebook handler.
        // Grove-specific initializers:
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, FacebookLoginConfiguration>();
        services.AddTransient<IConfigureOptions<FacebookOptions>, FacebookLoginConfiguration>();

        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<FacebookOptions>, OAuthPostConfigureOptions<FacebookOptions, FacebookHandler>>();
    }
}

[RequireFeatures("Grove.Deployment")]
public class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<FacebookLoginDeploymentSource, FacebookLoginDeploymentStep, FacebookLoginDeploymentStepDriver>();
    }
}
