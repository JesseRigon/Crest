using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes.RecipeSteps;
using Grove.Infrastructure.Recipes.Services;
using Grove.Security.Permissions;

namespace Grove.Infrastructure.Recipes;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.RecipesApi>();
        services.AddPermissionProvider<RecipesPermissionProvider>();
        services.AddRecipeExecutionStep<CommandStep>();
        services.AddRecipeExecutionStep<RecipesStep>();
        services.AddRecipeExecutionStep<ReloadTenantStep>();

        services.AddDeploymentTargetHandler<RecipeDeploymentTargetHandler>();
    }
}

[Feature("Grove.Infrastructure.Recipes.Core")]
public sealed class RecipesCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipes();
    }
}
