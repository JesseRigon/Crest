using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.CustomSettings.Deployment;
using Crest.CustomSettings.Drivers;
using Crest.CustomSettings.Recipes;
using Crest.CustomSettings.Services;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.CustomSettings;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<CustomSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<CustomSettingsService>();
        services.AddScoped<IStereotypesProvider, CustomSettingsStereotypesProvider>();
        // Permissions
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<Crest.Access.IResourcePermissionMapper, CustomSettingsPermissionMapper>();

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

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<CustomSettingsDeploymentSource, CustomSettingsDeploymentStep, CustomSettingsDeploymentStepDriver>();
    }
}
