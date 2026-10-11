using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.Deployment.Deployment;
using Crest.Deployment.Drivers;
using Crest.Deployment.Indexes;
using Crest.Deployment.Recipes;
using Crest.Deployment.Steps;
using Crest.DisplayManagement.Handlers;
using Crest.FileStorage;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.Deployment;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddTransient<FileCreationService>();
        services.AddDeploymentServices();

        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IDeploymentTargetProvider, FileDownloadDeploymentTargetProvider>();

        // Register the fallback type for unknown deployment steps (e.g., when a feature is disabled).
        services.AddJsonDerivedTypeFallback<DeploymentStep, UnknownDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, UnknownDeploymentStepDriver>();

        // Custom File deployment step
        services.AddDeployment<CustomFileDeploymentSource, CustomFileDeploymentStep, CustomFileDeploymentStepDriver>();

        // Recipe File deployment step
        services.AddDeploymentWithoutSource<RecipeFileDeploymentStep, RecipeFileDeploymentStepDriver>();

        services.AddIndexProvider<DeploymentPlanIndexProvider>();
        services.AddDataMigration<Migrations>();

        services.AddScoped<IDeploymentPlanService, DeploymentPlanService>();

        services.AddRecipeExecutionStep<DeploymentPlansRecipeStep>();

        services.AddDeployment<DeploymentPlanDeploymentSource, DeploymentPlanDeploymentStep, DeploymentPlanDeploymentStepDriver>();

        services.AddDeployment<JsonRecipeDeploymentSource, JsonRecipeDeploymentStep, JsonRecipeDeploymentStepDriver>();
    }
}
