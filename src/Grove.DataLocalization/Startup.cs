using Microsoft.Extensions.DependencyInjection;
using Grove.DataLocalization.Deployment;
using Grove.DataLocalization.Liquid;
using Grove.DataLocalization.Recipes;
using Grove.DataLocalization.Services;
using Grove.Deployment;
using Grove.Liquid;
using Grove.Localization.Data;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.DataLocalization;

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
        services.AddScoped<ITranslationsManager>(sp => sp.GetRequiredService<TranslationsManager>());
        services.AddScoped<Grove.Localization.IClientStringOverrides, ClientStringOverrides>();
        services.AddApiContributor<Api.TranslationsApi>();
        services.AddScoped<Grove.AdminMenu.IAdminMenuEventHandler, AdminMenuTranslationCleanup>();
        services.AddScoped<Grove.ContentTypes.Events.IContentDefinitionEventHandler, ContentTypeTranslationCleanup>();
        services.AddRecipeExecutionStep<TranslationsStep>();

        services.AddDeployment<TranslationsDeploymentSource, TranslationsDeploymentStep, TranslationsDeploymentStepDriver>();
        services.AddDeployment<AllDataTranslationsDeploymentSource, AllDataTranslationsDeploymentStep, AllDataTranslationsDeploymentStepDriver>();

        services.AddScoped<IPermissionProvider, Permissions>();

        services.AddDataLocalization();
        services.AddSingleton<IDataTranslationProvider, DataTranslationProvider>();
    }
}
