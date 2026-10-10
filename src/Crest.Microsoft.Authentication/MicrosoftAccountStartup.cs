using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Microsoft.Authentication.Configuration;
using Crest.Microsoft.Authentication.Deployment;
using Crest.Microsoft.Authentication.Drivers;
using Crest.Microsoft.Authentication.Recipes;
using Crest.Microsoft.Authentication.Services;
using Crest.Microsoft.Authentication.Settings;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.Microsoft.Authentication;

[Feature(MicrosoftAuthenticationConstants.Features.MicrosoftAccount)]
public sealed class MicrosoftAccountStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IMicrosoftAccountService, MicrosoftAccountService>();
        services.AddSiteDisplayDriver<MicrosoftAccountSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenuMicrosoftAccount>();

        services.AddRecipeExecutionStep<MicrosoftAccountSettingsStep>();

        services.AddTransient<IConfigureOptions<MicrosoftAccountSettings>, MicrosoftAccountSettingsConfiguration>();
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, MicrosoftAccountOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<MicrosoftAccountOptions>, MicrosoftAccountOptionsConfiguration>();
        services.AddTransient<IPostConfigureOptions<MicrosoftAccountOptions>, OAuthPostConfigureOptions<MicrosoftAccountOptions, MicrosoftAccountHandler>>();
    }
}

[RequireFeatures("Crest.Deployment")]
[Feature(MicrosoftAuthenticationConstants.Features.MicrosoftAccount)]
public sealed class MicrosoftAccountDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<MicrosoftAccountDeploymentSource, MicrosoftAccountDeploymentStep, MicrosoftAccountDeploymentStepDriver>();
    }
}
