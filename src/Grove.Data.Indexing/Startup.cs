using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.BackgroundTasks;
using Grove.Contents.Management.Handlers;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Data.Indexing;
using Grove.Data.Indexing.Deployments;
using Grove.Data.Indexing.Handlers;
using Grove.Data.Indexing.Recipes;
using Grove.Data.Indexing.DataMigrations;
using Grove.Data.Indexing.Deployments;
using Grove.Data.Indexing.Indexing;
using Grove.Data.Indexing.Models;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Search.Indexing.Core;
using Grove.Security.Permissions;

namespace Grove.Data.Indexing;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexingCore();
        services.AddDataMigration<RecordIndexingTaskMigrations>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddDataMigration<Migrations>();
#pragma warning restore CS0618 // Type or member is obsolete

        services.AddPermissionProvider<IndexingPermissionsProvider>();
        services.AddApiContributor<Api.IndexesApi>();
        services.AddDataMigration<PreviewIndexingMigrations>();

        services
            .AddIndexProvider<IndexProfileIndexProvider>()
            .AddDataMigration<IndexingMigrations>();
    }
}

[RequireFeatures("Grove.Contents")]
public sealed class ContentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContentHandler, IndexingContentHandler>());
        services.AddScoped<IContentHandler, CreateIndexingTaskContentHandler>();
        services.TryAddScoped<ContentIndexingService>();
        services.AddIndexProfileHandler<ContentIndexProfileHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IModularTenantEvents, ContentIndexInitializerService>());
        services.AddDataMigration<WorkerFeatureMigrations>();
    }
}

[RequireFeatures("Grove.Infrastructure.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<CreateOrUpdateIndexProfileStep>();
        services.AddRecipeExecutionStep<ResetIndexStep>();
        services.AddRecipeExecutionStep<RebuildIndexStep>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<IndexProfileDeploymentSource, IndexProfileDeploymentStep>();
        services.AddDeployment<RebuildIndexDeploymentSource, RebuildIndexDeploymentStep>();
        services.AddDeployment<ResetIndexDeploymentSource, ResetIndexDeploymentStep>();
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
