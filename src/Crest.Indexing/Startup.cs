using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.BackgroundTasks;
using Crest.ContentManagement.Handlers;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Indexing;
using Crest.Indexing.Deployments;
using Crest.Indexing.Handlers;
using Crest.Indexing.Recipes;
using Crest.Indexing.DataMigrations;
using Crest.Indexing.Deployments;
using Crest.Indexing.Drivers;
using Crest.Indexing.Indexing;
using Crest.Indexing.Models;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Search.Indexing.Core;
using Crest.Security.Permissions;

namespace Crest.Indexing;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexingCore();
        services.AddDataMigration<RecordIndexingTaskMigrations>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddDataMigration<Migrations>();
#pragma warning restore CS0618 // Type or member is obsolete

        services.AddNavigationProvider<AdminMenu>();
        services.AddDisplayDriver<IndexProfile, IndexProfileDisplayDriver>();
        services.AddPermissionProvider<IndexingPermissionsProvider>();
        services.AddDataMigration<PreviewIndexingMigrations>();

        services
            .AddIndexProvider<IndexProfileIndexProvider>()
            .AddDataMigration<IndexingMigrations>();
    }
}

[RequireFeatures("Crest.Contents")]
public sealed class ContentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContentHandler, IndexingContentHandler>());
        services.AddScoped<IContentHandler, CreateIndexingTaskContentHandler>();
        services.TryAddScoped<ContentIndexingService>();
        services.AddIndexProfileHandler<ContentIndexProfileHandler>();
        services.AddDisplayDriver<IndexProfile, ContentIndexProfileDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IModularTenantEvents, ContentIndexInitializerService>());
        services.AddDataMigration<WorkerFeatureMigrations>();
    }
}

[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<CreateOrUpdateIndexProfileStep>();
        services.AddRecipeExecutionStep<ResetIndexStep>();
        services.AddRecipeExecutionStep<RebuildIndexStep>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<IndexProfileDeploymentSource, IndexProfileDeploymentStep, IndexProfileDeploymentStepDisplayDriver>();
        services.AddDeployment<RebuildIndexDeploymentSource, RebuildIndexDeploymentStep, RebuildIndexDeploymentStepDriver>();
        services.AddDeployment<ResetIndexDeploymentSource, ResetIndexDeploymentStep, ResetIndexDeploymentStepDriver>();
    }
}

[Feature(IndexingConstants.Feature.Worker)]
public sealed class WorkerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTask, ContentIndexingBackgroundTask>();
    }
}
