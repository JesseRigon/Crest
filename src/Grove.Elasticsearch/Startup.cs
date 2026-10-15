using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.ContentManagement;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Indexing;
using Grove.Indexing.Models;
using Grove.Modules;
using Grove.Navigation;
using Grove.Queries;
using Grove.Queries;
using Grove.Queries.Sql.Migrations;
using Grove.Recipes;
using Grove.Search;
using Grove.Elasticsearch.Deployment;
using Grove.Elasticsearch.Handlers;
using Grove.Elasticsearch.Models;
using Grove.Elasticsearch.Providers;
using Grove.Elasticsearch.Recipes;
using Grove.Elasticsearch.Services;
using Grove.Elasticsearch.Drivers;
using Grove.Elasticsearch.Services;
using Grove.Environment.Shell.Scope;
using Grove.Security.Permissions;
using Grove.Search.Elasticsearch.Migrations;

namespace Grove.Elasticsearch;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<ElasticsearchConnectionOptions>, ElasticsearchConnectionOptionsConfigurations>();
        services.AddTransient<IElasticsearchClientFactory, ElasticsearchClientFactory>();
        services.AddSingleton((sp) =>
        {
            var factory = sp.GetRequiredService<IElasticsearchClientFactory>();
            var options = sp.GetRequiredService<IOptions<ElasticsearchConnectionOptions>>().Value;

            return factory.Create(options);
        });

        services.Configure<ElasticsearchOptions>(options =>
        {
            var configuration = _shellConfiguration.GetSection(ElasticsearchConnectionOptionsConfigurations.ConfigSectionName);

            options.AddIndexPrefix(configuration);
            options.AddTokenFilters(configuration);
            options.AddAnalyzers(configuration);
        });

        services.AddElasticsearchServices();
        services.AddPermissionProvider<PermissionProvider>();
        services.AddPageSource<AdminPages>();
        services.AddDisplayDriver<Query, ElasticsearchQueryDisplayDriver>();
        services.AddDataMigration<ElasticsearchQueryMigrations>();
        services.AddScoped<IQueryHandler, ElasticsearchQueryHandler>();

        services.AddDisplayDriver<IndexProfile, ElasticsearchIndexProfileDisplayDriver>();

        services.AddIndexProfileHandler<ElasticsearchIndexProfileHandler>();
    }
}

[RequireFeatures("Grove.Recipes.Core")]
public sealed class RecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ElasticsearchIndexStep>();
        services.AddRecipeExecutionStep<ElasticsearchIndexRebuildStep>();
        services.AddRecipeExecutionStep<ElasticsearchIndexResetStep>();
    }
}

[RequireFeatures("Grove.Contents")]
public sealed class ContentsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsStartup(IStringLocalizer<ContentsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<IndexingMigrations>();

        // Register after IndexingMigrations so its deferred task, which rewrites obsolete per-index role
        // permissions to the new dynamic permissions, runs after the index profiles have been created.
        services.AddDataMigration<PermissionMigrations>();

        services
            .AddIndexProfileHandler<ElasticsearchContentIndexProfileHandler>()
            .AddElasticsearchIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in Elasticsearch"];
                o.Description = S["Create an Elasticsearch index based on site contents."];
            });
    }
}

[RequireFeatures("Grove.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<ElasticsearchService>(ElasticsearchConstants.ProviderName);
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ElasticsearchIndexDeploymentSource, ElasticsearchIndexDeploymentStep, ElasticIndexDeploymentStepDriver>();
        services.AddDeployment<ElasticsearchIndexRebuildDeploymentSource, ElasticsearchIndexRebuildDeploymentStep, ElasticIndexRebuildDeploymentStepDriver>();
        services.AddDeployment<ElasticsearchIndexResetDeploymentSource, ElasticsearchIndexResetDeploymentStep, ElasticIndexResetDeploymentStepDriver>();
    }
}

[Feature("Grove.Search.Elasticsearch.ContentPicker")]
public sealed class ElasticContentPickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentPickerResultProvider, ElasticsearchContentPickerResultProvider>();
        services.AddShapeAttributes<ElasticContentPickerShapeProvider>();
    }
}

[RequireFeatures("Grove.ContentTypes")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}

[Feature("Grove.Search.Elasticsearch")]
public sealed class LegacyFeatureStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<LegacyFeatureMigrations>();
    }
}

internal sealed class LegacyFeatureMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var featuresManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await featuresManager.IsFeatureEnabledAsync("Grove.Elasticsearch"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Grove.Elasticsearch");
        });

        return 1;
    }
}
