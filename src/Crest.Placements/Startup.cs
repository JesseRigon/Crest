using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.ContentTypes.Editors;
using Crest.Deployment;
using Crest.DisplayManagement.Descriptors;
using Crest.Modules;
using Crest.Navigation;
using Crest.Placements.Deployment;
using Crest.Placements.Recipes;
using Crest.Placements.Services;
using Crest.Placements.Settings;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.Placements;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();

        services.TryAddScoped<IPlacementStore, DatabasePlacementsStore>();
        services.AddScoped<PlacementsManager>();
        services.AddScoped<IShapePlacementProvider, PlacementProvider>();

        // Shortcuts in settings
        services.AddScoped<IContentPartDefinitionDisplayDriver, PlacementContentPartDefinitionDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, PlacementContentTypePartDefinitionDriver>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, PlacementContentPartFieldDefinitionDisplayDriver>();

        // Recipes
        services.AddRecipeExecutionStep<PlacementStep>();
    }
}

[Feature("Crest.Placements.FileStorage")]
public class FileContentDefinitionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IPlacementStore>();
        services.AddScoped<IPlacementStore, FilePlacementsStore>();
    }
}

[RequireFeatures("Crest.Deployment")]
public class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<PlacementsDeploymentSource, PlacementsDeploymentStep, PlacementsDeploymentStepDriver>();
    }
}
