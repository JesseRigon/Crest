using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Infrastructure.CustomSettings.Deployment;
using Grove.Infrastructure.CustomSettings.Recipes;
using Grove.Infrastructure.CustomSettings.Services;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Infrastructure.CustomSettings;

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
        services.AddDeployment<CustomSettingsDeploymentSource, CustomSettingsDeploymentStep>();
    }
}
