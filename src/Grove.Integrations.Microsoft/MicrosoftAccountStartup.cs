using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Integrations.Microsoft.Configuration;
using Grove.Integrations.Microsoft.Deployment;
using Grove.Integrations.Microsoft.Recipes;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Integrations.Microsoft;

[Feature(MicrosoftAuthenticationConstants.Features.MicrosoftAccount)]
public sealed class MicrosoftAccountStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IMicrosoftAccountService, MicrosoftAccountService>();
        services.AddSettingsGroup<MicrosoftAccountSettings>(
            MicrosoftAuthenticationConstants.Features.MicrosoftAccount,
            "Microsoft",
            Permissions.ManageMicrosoftAuthentication,
            group => group.InMenu("settings-authentication", entryId: "microsoft").ReloadsTenant());

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
        services.AddDeployment<MicrosoftAccountDeploymentSource, MicrosoftAccountDeploymentStep>();
    }
}
