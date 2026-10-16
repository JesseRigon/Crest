using Grove.Security.Permissions;
using Grove.Integrations.AI.Azure.Models;
using Grove.Environment.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Integrations.AI.Azure;
using Grove.Integrations.AI.Azure.Deployment;
using Grove.Integrations.AI.Azure.Flows;
using Grove.Integrations.AI.Azure.Handlers;
using Grove.Integrations.AI.Azure.Recipes;
using Grove.Integrations.AI.Azure.Services;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Data.Indexing;
using Grove.Data.Indexing.Models;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Search;
using Grove.Search.AzureAI.Migrations;

namespace Grove.Integrations.AI.Azure;

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
        services.AddSettingsGroup<AzureAISearchDefaultSettings>(AzureAISearchDefaultSettings.GroupId, "Azure AI Search", IndexingPermissions.ManageIndexes, group => group
            .InMenu("settings-search", entryId: "azureaisearch")
            .AlsoAdmits(AzureAISearchPermissions.ManageAzureAISearchISettings)
            .Describe("The search service's endpoint and key are a connection on the Connections screen.")
            .ValidatedBy((provider, settings, errors, cancellationToken) =>
            {
                settings.Connection = string.IsNullOrWhiteSpace(settings.Connection) ? null : settings.Connection.Trim();
                return Task.CompletedTask;
            })
            .OnSaved((provider, _, _) =>
            {
                provider.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<AzureAISearchDefaultOptions>();
                return Task.CompletedTask;
            }));
        services.AddScoped<Grove.Data.Connectors.IConnectionChangeHandler, AzureAISearchConnectionChangeHandler>();
        services.AddDataMigration<AzureAISearchConnectionMigrations>();
        services.AddAzureAISearchServices();
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

[RequireFeatures("Grove.Contents.Types")]
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

        services
            .AddIndexProfileHandler<AzureAISearchContentIndexProfileHandler>()
            .AddAzureAISearchIndexingSource(IndexingConstants.ContentsIndexSource, o =>
            {
                o.DisplayName = S["Content in Azure AI Search"];
                o.Description = S["Create an Azure AI Search index based on site contents."];
            });
    }
}

[RequireFeatures("Grove.Infrastructure.Recipes.Core")]
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
        services.AddDeployment<AzureAISearchIndexDeploymentSource, AzureAISearchIndexDeploymentStep>();
        services.AddDeployment<AzureAISearchIndexRebuildDeploymentSource, AzureAISearchIndexRebuildDeploymentStep>();
        services.AddDeployment<AzureAISearchIndexResetDeploymentSource, AzureAISearchIndexResetDeploymentStep>();
    }
}

[RequireFeatures("Grove.Contents.BagParts")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Grove.Integrations.AI.Azure"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Grove.Integrations.AI.Azure");
        });

        return 1;
    }
}
