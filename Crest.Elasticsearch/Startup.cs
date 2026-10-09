using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.ContentManagement;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Indexing.Core;
using Crest.Indexing.Models;
using Crest.Modules;
using Crest.Navigation;
using Crest.Queries;
using Crest.Queries.Core;
using Crest.Queries.Sql.Migrations;
using Crest.Recipes;
using Crest.Search;
using Crest.Elasticsearch.Core.Deployment;
using Crest.Elasticsearch.Core.Handlers;
using Crest.Elasticsearch.Core.Models;
using Crest.Elasticsearch.Core.Providers;
using Crest.Elasticsearch.Core.Recipes;
using Crest.Elasticsearch.Core.Services;
using Crest.Elasticsearch.Drivers;
using Crest.Elasticsearch.Services;
using Crest.Environment.Shell.Scope;
using Crest.Security.Permissions;
using Crest.Search.Elasticsearch.Migrations;

namespace Crest.Elasticsearch;

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
        services.AddNavigationProvider<AdminMenu>();
        services.AddDisplayDriver<Query, ElasticsearchQueryDisplayDriver>();
        services.AddDataMigration<ElasticsearchQueryMigrations>();
        services.AddScoped<IQueryHandler, ElasticsearchQueryHandler>();

        services.AddDisplayDriver<IndexProfile, ElasticsearchIndexProfileDisplayDriver>();

        services.AddIndexProfileHandler<ElasticsearchIndexProfileHandler>();
    }
}

[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ElasticsearchIndexStep>();
        services.AddRecipeExecutionStep<ElasticsearchIndexRebuildStep>();
        services.AddRecipeExecutionStep<ElasticsearchIndexResetStep>();
    }
}

[RequireFeatures("Crest.Contents")]
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

[RequireFeatures("Crest.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<ElasticsearchService>(ElasticsearchConstants.ProviderName);
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ElasticsearchIndexDeploymentSource, ElasticsearchIndexDeploymentStep, ElasticIndexDeploymentStepDriver>();
        services.AddDeployment<ElasticsearchIndexRebuildDeploymentSource, ElasticsearchIndexRebuildDeploymentStep, ElasticIndexRebuildDeploymentStepDriver>();
        services.AddDeployment<ElasticsearchIndexResetDeploymentSource, ElasticsearchIndexResetDeploymentStep, ElasticIndexResetDeploymentStepDriver>();
    }
}

[Feature("Crest.Search.Elasticsearch.ContentPicker")]
public sealed class ElasticContentPickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentPickerResultProvider, ElasticsearchContentPickerResultProvider>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, ContentPickerFieldElasticEditorSettingsDriver>();
        services.AddShapeAttributes<ElasticContentPickerShapeProvider>();
    }
}

[RequireFeatures("Crest.ContentTypes")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ContentTypePartIndexSettingsDisplayDriver>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, ContentPartFieldIndexSettingsDisplayDriver>();
    }
}

[Feature("Crest.Search.Elasticsearch")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Crest.Elasticsearch"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Crest.Elasticsearch");
        });

        return 1;
    }
}
