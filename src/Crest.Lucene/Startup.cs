using Lucene.Net.Analysis.Standard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Indexing;
using Crest.Indexing.Models;
using Crest.Lucene;
using Crest.Lucene.Handlers;
using Crest.Lucene.Deployment;
using Crest.Lucene.Drivers;
using Crest.Lucene.Recipes;
using Crest.Lucene.Services;
using Crest.Lucene.Settings;
using Crest.Modules;
using Crest.Navigation;
using Crest.Queries;
using Crest.Queries;
using Crest.Queries.Sql.Migrations;
using Crest.Recipes;
using Crest.Search;
using Crest.Search.Lucene.DataMigrations;
using Crest.Security.Permissions;

namespace Crest.Lucene;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Crest.Search.Lucene.Migrations>();
        services.TryAddSingleton<ILuceneIndexStore, LuceneIndexStore>();
        services.TryAddSingleton<ILuceneIndexingState, LuceneIndexingState>();
        services.TryAddSingleton<LuceneAnalyzerManager>();
        services.TryAddScoped<ILuceneSearchQueryService, LuceneSearchQueryService>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();

        services.Configure<LuceneOptions>(o =>
            o.Analyzers.Add(new LuceneAnalyzer(LuceneConstants.DefaultAnalyzer,
                new StandardAnalyzer(LuceneConstants.DefaultVersion))));

        services.AddDisplayDriver<Query, LuceneQueryDisplayDriver>();

        services
            .AddLuceneQueries()
            .AddQuerySource<LuceneQuerySource>(LuceneQuerySource.SourceName);

        services.AddDataMigration<LuceneQueryMigrations>();
        services.AddScoped<IQueryHandler, LuceneQueryHandler>();

        services.AddDisplayDriver<IndexProfile, LuceneIndexProfileDisplayDriver>();

        services.AddIndexProfileHandler<LuceneIndexProfileHandler>();
    }
}

[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LuceneIndexStep>();
        services.AddRecipeExecutionStep<LuceneIndexRebuildStep>();
        services.AddRecipeExecutionStep<LuceneIndexResetStep>();
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
            .AddIndexProfileHandler<LuceneContentIndexProfileHandler>()
            .AddLuceneIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in Lucene"];
                o.Description = S["Create an Lucene index based on site contents."];
            });
    }
}

[RequireFeatures("Crest.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<LuceneSearchService>(LuceneConstants.ProviderName);
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LuceneIndexDeploymentSource, LuceneIndexDeploymentStep, LuceneIndexDeploymentStepDriver>();
        services.AddDeployment<LuceneIndexRebuildDeploymentSource, LuceneIndexRebuildDeploymentStep, LuceneIndexRebuildDeploymentStepDriver>();
        services.AddDeployment<LuceneIndexResetDeploymentSource, LuceneIndexResetDeploymentStep, LuceneIndexResetDeploymentStepDriver>();
    }
}

[Feature("Crest.Search.Lucene.ContentPicker")]
public sealed class LuceneContentPickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentPickerResultProvider, LuceneContentPickerResultProvider>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, ContentPickerFieldLuceneEditorSettingsDriver>();
        services.AddShapeAttributes<LuceneContentPickerShapeProvider>();
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

[Feature("Crest.Search.Lucene")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Crest.Lucene"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Crest.Lucene");
        });

        return 1;
    }
}
