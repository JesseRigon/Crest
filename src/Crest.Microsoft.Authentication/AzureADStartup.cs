using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
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

[Feature(MicrosoftAuthenticationConstants.Features.AAD)]
public sealed class AzureADStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IAzureADService, AzureADService>();
        services.AddRecipeExecutionStep<AzureADSettingsStep>();

        services.AddSiteDisplayDriver<AzureADSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenuAAD>();

        services.AddTransient<IConfigureOptions<AzureADSettings>, AzureADSettingsConfiguration>();

        services.AddTransient<IConfigureOptions<AuthenticationOptions>, AzureADOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<MicrosoftIdentityOptions>, AzureADOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<PolicySchemeOptions>, AzureADOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<OpenIdConnectOptions>, OpenIdConnectOptionsConfiguration>();

        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>();
    }
}

[RequireFeatures("Crest.Deployment")]
[Feature(MicrosoftAuthenticationConstants.Features.AAD)]
public sealed class AzureADDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AzureADDeploymentSource, AzureADDeploymentStep, AzureADDeploymentStepDriver>();
    }
}
