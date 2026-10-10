using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes.RecipeSteps;
using Crest.Recipes.Services;
using Crest.Security.Permissions;

namespace Crest.Recipes;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<RecipesPermissionProvider>();
        services.AddRecipeExecutionStep<CommandStep>();
        services.AddRecipeExecutionStep<RecipesStep>();
        services.AddRecipeExecutionStep<ReloadTenantStep>();

        services.AddDeploymentTargetHandler<RecipeDeploymentTargetHandler>();
    }
}

[Feature("Crest.Recipes.Core")]
public sealed class RecipesCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipes();
    }
}
