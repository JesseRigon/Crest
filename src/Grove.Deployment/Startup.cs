using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Deployment.Deployment;
using Grove.Deployment.Indexes;
using Grove.Deployment.Recipes;
using Grove.Deployment.Steps;
using Grove.DisplayManagement.Handlers;
using Grove.FileStorage;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.Deployment;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.DeploymentApi>();
        services.TryAddTransient<FileCreationService>();
        services.AddDeploymentServices();

        services.AddPermissionProvider<Permissions>();


        // Register the fallback type for unknown deployment steps (e.g., when a feature is disabled).
        services.AddJsonDerivedTypeFallback<DeploymentStep, UnknownDeploymentStep>();

        // Custom File deployment step
        services.AddDeployment<CustomFileDeploymentSource, CustomFileDeploymentStep>();

        // Recipe File deployment step
        services.AddDeploymentWithoutSource<RecipeFileDeploymentStep>();

        services.AddIndexProvider<DeploymentPlanIndexProvider>();
        services.AddDataMigration<Migrations>();

        services.AddScoped<IDeploymentPlanService, DeploymentPlanService>();

        services.AddRecipeExecutionStep<DeploymentPlansRecipeStep>();

        services.AddDeployment<DeploymentPlanDeploymentSource, DeploymentPlanDeploymentStep>();

        services.AddDeployment<JsonRecipeDeploymentSource, JsonRecipeDeploymentStep>();
    }
}
