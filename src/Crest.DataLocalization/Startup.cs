using Microsoft.Extensions.DependencyInjection;
using Crest.DataLocalization.Deployment;
using Crest.DataLocalization.Liquid;
using Crest.DataLocalization.Recipes;
using Crest.DataLocalization.Services;
using Crest.Deployment;
using Crest.Liquid;
using Crest.Localization.Data;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.DataLocalization;

/// <summary>
/// Represents a localization module entry point.
/// </summary>
public class Startup : StartupBase
{
    public override int ConfigureOrder => -100;

    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<DataLocalizationFilter>("d");

        services.AddScoped<TranslationsManager>();
        services.AddRecipeExecutionStep<TranslationsStep>();

        services.AddDeployment<TranslationsDeploymentSource, TranslationsDeploymentStep, TranslationsDeploymentStepDriver>();
        services.AddDeployment<AllDataTranslationsDeploymentSource, AllDataTranslationsDeploymentStep, AllDataTranslationsDeploymentStepDriver>();

        services.AddScoped<IPermissionProvider, Permissions>();
        services.AddScoped<INavigationProvider, AdminMenu>();

        services.AddDataLocalization();
        services.AddSingleton<IDataTranslationProvider, DataTranslationProvider>();
    }
}
