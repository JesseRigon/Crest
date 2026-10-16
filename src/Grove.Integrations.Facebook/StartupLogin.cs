using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Data.Connectors;
using Grove.Deployment;
using Grove.Environment.Shell;
using Grove.Integrations.Facebook.Services;
using Grove.Display.Management.Handlers;
using Grove.Integrations.Facebook.Deployment;
using Grove.Integrations.Facebook.Login.Configuration;
using Grove.Integrations.Facebook.Login.Recipes;
using Grove.Integrations.Facebook.Login.Services;
using Grove.Integrations.Facebook.Login.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;

namespace Grove.Integrations.Facebook;

[Feature(FacebookConstants.Features.Login)]
public sealed class StartupLogin : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFacebookLoginService, FacebookLoginService>();
        services.AddSettingsGroup<FacebookLoginSettings>(
            FacebookConstants.Features.Login,
            "Meta",
            Permissions.ManageFacebookApp,
            group => group.InMenu("settings-authentication", entryId: "facebook").ReloadsTenant());
        services.AddRecipeExecutionStep<FacebookLoginSettingsStep>();
        services.AddScoped<IConnectionChangeHandler>(sp => new AppCredentialsChangeHandler(
            sp.GetRequiredService<IShellReleaseManager>(), FacebookConstants.AppCredentialsKey, FacebookConstants.GraphConnectionKey));

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
        services.AddDeployment<FacebookLoginDeploymentSource, FacebookLoginDeploymentStep>();
    }
}
