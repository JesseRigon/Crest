using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Microsoft.Authentication.Configuration;
using Grove.Microsoft.Authentication.Deployment;
using Grove.Microsoft.Authentication.Drivers;
using Grove.Microsoft.Authentication.Recipes;
using Grove.Microsoft.Authentication.Services;
using Grove.Microsoft.Authentication.Settings;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.Microsoft.Authentication;

[Feature(MicrosoftAuthenticationConstants.Features.MicrosoftAccount)]
public sealed class MicrosoftAccountStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IMicrosoftAccountService, MicrosoftAccountService>();
        services.AddSiteDisplayDriver<MicrosoftAccountSettingsDisplayDriver>();
        services.AddPageSource<MicrosoftAccountPages>();

        services.AddRecipeExecutionStep<MicrosoftAccountSettingsStep>();

        services.AddTransient<IConfigureOptions<MicrosoftAccountSettings>, MicrosoftAccountSettingsConfiguration>();
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, MicrosoftAccountOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<MicrosoftAccountOptions>, MicrosoftAccountOptionsConfiguration>();
        services.AddTransient<IPostConfigureOptions<MicrosoftAccountOptions>, OAuthPostConfigureOptions<MicrosoftAccountOptions, MicrosoftAccountHandler>>();
    }
}

[RequireFeatures("Grove.Deployment")]
[Feature(MicrosoftAuthenticationConstants.Features.MicrosoftAccount)]
public sealed class MicrosoftAccountDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<MicrosoftAccountDeploymentSource, MicrosoftAccountDeploymentStep, MicrosoftAccountDeploymentStepDriver>();
    }
}
