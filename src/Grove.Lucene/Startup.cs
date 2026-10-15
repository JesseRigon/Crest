using Lucene.Net.Analysis.Standard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Grove.ContentManagement;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Indexing;
using Grove.Indexing.Models;
using Grove.Lucene;
using Grove.Lucene.Handlers;
using Grove.Lucene.Deployment;
using Grove.Lucene.Recipes;
using Grove.Lucene.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Queries;
using Grove.Queries;
using Grove.Queries.Sql.Migrations;
using Grove.Recipes;
using Grove.Search;
using Grove.Search.Lucene.DataMigrations;
using Grove.Security.Permissions;

namespace Grove.Lucene;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Grove.Search.Lucene.Migrations>();
        services.TryAddSingleton<ILuceneIndexStore, LuceneIndexStore>();
        services.TryAddSingleton<ILuceneIndexingState, LuceneIndexingState>();
        services.TryAddSingleton<LuceneAnalyzerManager>();
        services.TryAddScoped<ILuceneSearchQueryService, LuceneSearchQueryService>();
        services.AddApiContributor<Api.LuceneQueryApi>();
        services.AddPermissionProvider<Permissions>();

        services.Configure<LuceneOptions>(o =>
            o.Analyzers.Add(new LuceneAnalyzer(LuceneConstants.DefaultAnalyzer,
                new StandardAnalyzer(LuceneConstants.DefaultVersion))));


        services
            .AddLuceneQueries()
            .AddQuerySource<LuceneQuerySource>(LuceneQuerySource.SourceName);

        services.AddDataMigration<LuceneQueryMigrations>();
        services.AddScoped<IQueryHandler, LuceneQueryHandler>();


        services.AddIndexProfileHandler<LuceneIndexProfileHandler>();
    }
}

[RequireFeatures("Grove.Recipes.Core")]
public sealed class RecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LuceneIndexStep>();
        services.AddRecipeExecutionStep<LuceneIndexRebuildStep>();
        services.AddRecipeExecutionStep<LuceneIndexResetStep>();
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
            .AddIndexProfileHandler<LuceneContentIndexProfileHandler>()
            .AddLuceneIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in Lucene"];
                o.Description = S["Create an Lucene index based on site contents."];
            });
    }
}

[RequireFeatures("Grove.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<LuceneSearchService>(LuceneConstants.ProviderName);
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LuceneIndexDeploymentSource, LuceneIndexDeploymentStep>();
        services.AddDeployment<LuceneIndexRebuildDeploymentSource, LuceneIndexRebuildDeploymentStep>();
        services.AddDeployment<LuceneIndexResetDeploymentSource, LuceneIndexResetDeploymentStep>();
    }
}

[RequireFeatures("Grove.ContentTypes")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}

[Feature("Grove.Search.Lucene")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Grove.Lucene"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Grove.Lucene");
        });

        return 1;
    }
}
