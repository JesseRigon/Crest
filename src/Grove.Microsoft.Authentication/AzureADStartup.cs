using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Microsoft.Authentication.Configuration;
using Grove.Microsoft.Authentication.Deployment;
using Grove.Microsoft.Authentication.Recipes;
using Grove.Microsoft.Authentication.Services;
using Grove.Microsoft.Authentication.Settings;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.Microsoft.Authentication;

[Feature(MicrosoftAuthenticationConstants.Features.AAD)]
public sealed class AzureADStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IAzureADService, AzureADService>();
        services.AddRecipeExecutionStep<AzureADSettingsStep>();

        services.AddSettingsGroup<AzureADSettings>(
            MicrosoftAuthenticationConstants.Features.AAD,
            "Microsoft Entra ID",
            Permissions.ManageMicrosoftAuthentication,
            group => group.InMenu("settings-authentication", entryId: "microsoftentraid").ReloadsTenant());

        services.AddTransient<IConfigureOptions<AzureADSettings>, AzureADSettingsConfiguration>();

        services.AddTransient<IConfigureOptions<AuthenticationOptions>, AzureADOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<MicrosoftIdentityOptions>, AzureADOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<PolicySchemeOptions>, AzureADOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<OpenIdConnectOptions>, OpenIdConnectOptionsConfiguration>();

        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>();
    }
}

[RequireFeatures("Grove.Deployment")]
[Feature(MicrosoftAuthenticationConstants.Features.AAD)]
public sealed class AzureADDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AzureADDeploymentSource, AzureADDeploymentStep>();
    }
}
