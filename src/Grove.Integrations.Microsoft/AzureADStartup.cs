using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
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
