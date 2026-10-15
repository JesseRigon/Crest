using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Contents.Management;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Descriptors;
using Grove.Display.Management.Handlers;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Data.Indexing;
using Grove.Data.Indexing.Models;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Data.Queries;
using Grove.Data.Queries;
using Grove.Data.Queries.Sql.Migrations;
using Grove.Infrastructure.Recipes;
using Grove.Search;
using Grove.Search.Elasticsearch.Deployment;
using Grove.Search.Elasticsearch.Handlers;
using Grove.Search.Elasticsearch.Models;
using Grove.Search.Elasticsearch.Recipes;
using Grove.Search.Elasticsearch.Services;
using Grove.Search.Elasticsearch.Services;
using Grove.Environment.Shell.Scope;
using Grove.Security.Permissions;
using Grove.Search.Elasticsearch.Migrations;

namespace Grove.Search.Elasticsearch;

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
        services.AddApiContributor<Api.ElasticsearchQueryApi>();
        services.AddDataMigration<ElasticsearchQueryMigrations>();
        services.AddScoped<IQueryHandler, ElasticsearchQueryHandler>();


        services.AddIndexProfileHandler<ElasticsearchIndexProfileHandler>();
    }
}

[RequireFeatures("Grove.Infrastructure.Recipes.Core")]
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
        services.AddDeployment<ElasticsearchIndexDeploymentSource, ElasticsearchIndexDeploymentStep>();
        services.AddDeployment<ElasticsearchIndexRebuildDeploymentSource, ElasticsearchIndexRebuildDeploymentStep>();
        services.AddDeployment<ElasticsearchIndexResetDeploymentSource, ElasticsearchIndexResetDeploymentStep>();
    }
}

[RequireFeatures("Grove.Contents.Types")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}
