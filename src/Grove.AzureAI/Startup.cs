using Grove.Security.Permissions;
using Grove.AzureAI.Models;
using Grove.Environment.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.AzureAI;
using Grove.AzureAI.Deployment;
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
        services.AddSettingsGroup<AzureAISearchDefaultSettings>(AzureAISearchDefaultSettings.GroupId, "Azure AI Search", IndexingPermissions.ManageIndexes, group => group
            .InMenu("settings-search", entryId: "azureaisearch")
            .AlsoAdmits(AzureAISearchPermissions.ManageAzureAISearchISettings)
            .ValidatedBy((provider, settings, errors, cancellationToken) =>
            {
                var options = provider.GetRequiredService<IOptionsMonitor<AzureAISearchDefaultOptions>>().CurrentValue;
                if (options.DisableUIConfiguration)
                {
                    errors.Add(string.Empty, "The host configures Azure AI Search; these settings cannot be changed here.");
                    return Task.CompletedTask;
                }

                if (!options.FileConfigurationExists())
                {
                    settings.UseCustomConfiguration = true;
                }

                if (settings.UseCustomConfiguration)
                {
                    if (string.IsNullOrWhiteSpace(settings.Endpoint))
                    {
                        errors.Add(nameof(settings.Endpoint), "Endpoint is required.");
                    }
                    else if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _))
                    {
                        errors.Add(nameof(settings.Endpoint), "Endpoint must be a valid url.");
                    }

                    if (settings.AuthenticationType == AzureAIAuthenticationType.ApiKey && string.IsNullOrEmpty(settings.ApiKey))
                    {
                        errors.Add(nameof(settings.ApiKey), "API Key is required when using API Key authentication type.");
                    }

                    settings.IdentityClientId = settings.IdentityClientId?.Trim();
                }

                return Task.CompletedTask;
            })
            .OnSaved((provider, _, _) =>
            {
                provider.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<AzureAISearchDefaultOptions>();
                return Task.CompletedTask;
            }));
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
        services.AddDeployment<AzureAISearchIndexDeploymentSource, AzureAISearchIndexDeploymentStep>();
        services.AddDeployment<AzureAISearchIndexRebuildDeploymentSource, AzureAISearchIndexRebuildDeploymentStep>();
        services.AddDeployment<AzureAISearchIndexResetDeploymentSource, AzureAISearchIndexResetDeploymentStep>();
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
