using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.AzureAI.Core;
using Crest.AzureAI.Deployment;
using Crest.AzureAI.Drivers;
using Crest.AzureAI.Flows;
using Crest.AzureAI.Handlers;
using Crest.AzureAI.Recipes;
using Crest.AzureAI.Services;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Scope;
using Crest.Indexing.Core;
using Crest.Indexing.Models;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Search;
using Crest.Search.AzureAI.Migrations;

namespace Crest.AzureAI;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddIndexProfileHandler<AzureAISearchIndexProfileHandler>();
        services.AddIndexProfileHandler<AzureAISearchIndexHandler>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddAzureAISearchServices();
        services.AddSiteDisplayDriver<AzureAISearchDefaultSettingsDisplayDriver>();
        services.AddDataMigration<AzureAISearchIndexSettingsMigrations>();
        services.AddDataMigration<PermissionMigrations>();
    }
}

[RequireFeatures("Crest.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<AzureAISearchService>(AzureAISearchConstants.ProviderName);
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
        services.AddDisplayDriver<IndexProfile, AzureAISearchIndexProfileDisplayDriver>();

        services
            .AddIndexProfileHandler<AzureAISearchContentIndexProfileHandler>()
            .AddAzureAISearchIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in Azure AI Search"];
                o.Description = S["Create an Azure AI Search index based on site contents."];
            });
    }
}

[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AzureAISearchIndexRebuildStep>();
        services.AddRecipeExecutionStep<AzureAISearchIndexResetStep>();
        services.AddRecipeExecutionStep<AzureAISearchIndexSettingsStep>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AzureAISearchIndexDeploymentSource, AzureAISearchIndexDeploymentStep, AzureAISearchIndexDeploymentStepDriver>();
        services.AddDeployment<AzureAISearchIndexRebuildDeploymentSource, AzureAISearchIndexRebuildDeploymentStep, AzureAISearchIndexRebuildDeploymentStepDriver>();
        services.AddDeployment<AzureAISearchIndexResetDeploymentSource, AzureAISearchIndexResetDeploymentStep, AzureAISearchIndexResetDeploymentStepDriver>();
    }
}

[RequireFeatures("Crest.Flows")]
public sealed class FlowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAzureAISearchFieldIndexEvents, BagPartAzureAISearchFieldIndexEvents>();
    }
}

[Feature("Crest.Search.AzureAI")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Crest.AzureAI"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Crest.AzureAI");
        });

        return 1;
    }
}
