using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.AzureAI;
using Grove.AzureAI.Deployment;
using Grove.AzureAI.Drivers;
using Grove.AzureAI.Flows;
using Grove.AzureAI.Handlers;
using Grove.AzureAI.Recipes;
using Grove.AzureAI.Services;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Indexing;
using Grove.Indexing.Models;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Search;
using Grove.Search.AzureAI.Migrations;

namespace Grove.AzureAI;

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
        services.AddPageSource<AdminPages>();
        services.AddAzureAISearchServices();
        services.AddSiteDisplayDriver<AzureAISearchDefaultSettingsDisplayDriver>();
        services.AddDataMigration<AzureAISearchIndexSettingsMigrations>();
        services.AddDataMigration<PermissionMigrations>();
    }
}

[RequireFeatures("Grove.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSearchService<AzureAISearchService>(AzureAISearchConstants.ProviderName);
    }
}

[RequireFeatures("Grove.ContentTypes")]
public sealed class ContentTypesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
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

[RequireFeatures("Grove.Recipes.Core")]
public sealed class RecipeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AzureAISearchIndexRebuildStep>();
        services.AddRecipeExecutionStep<AzureAISearchIndexResetStep>();
        services.AddRecipeExecutionStep<AzureAISearchIndexSettingsStep>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AzureAISearchIndexDeploymentSource, AzureAISearchIndexDeploymentStep, AzureAISearchIndexDeploymentStepDriver>();
        services.AddDeployment<AzureAISearchIndexRebuildDeploymentSource, AzureAISearchIndexRebuildDeploymentStep, AzureAISearchIndexRebuildDeploymentStepDriver>();
        services.AddDeployment<AzureAISearchIndexResetDeploymentSource, AzureAISearchIndexResetDeploymentStep, AzureAISearchIndexResetDeploymentStepDriver>();
    }
}

[RequireFeatures("Grove.Flows")]
public sealed class FlowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAzureAISearchFieldIndexEvents, BagPartAzureAISearchFieldIndexEvents>();
    }
}

[Feature("Grove.Search.AzureAI")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Grove.AzureAI"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Grove.AzureAI");
        });

        return 1;
    }
}
