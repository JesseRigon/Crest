using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.CustomSettings.Deployment;
using Grove.CustomSettings.Recipes;
using Grove.CustomSettings.Services;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.CustomSettings;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDynamicPageSource<CustomSettingsPages>();
        services.AddScoped<CustomSettingsService>();
        services.AddApiContributor<Api.CustomSettingsApi>();
        services.AddScoped<IStereotypesProvider, CustomSettingsStereotypesProvider>();
        // Permissions
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<Grove.Access.IResourcePermissionMapper, CustomSettingsPermissionMapper>();

        services.AddRecipeExecutionStep<CustomSettingsStep>();

        services.Configure<ContentTypeDefinitionOptions>(options =>
        {
            options.Stereotypes.TryAdd("CustomSettings", new ContentTypeDefinitionDriverOptions
            {
                ShowCreatable = false,
                ShowListable = false,
                ShowDraftable = false,
                ShowVersionable = false,
            });
        });
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<CustomSettingsDeploymentSource, CustomSettingsDeploymentStep, CustomSettingsDeploymentStepDriver>();
    }
}
