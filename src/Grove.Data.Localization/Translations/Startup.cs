using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Localization.Translations.Deployment;
using Grove.Data.Localization.Translations.Liquid;
using Grove.Data.Localization.Translations.Recipes;
using Grove.Data.Localization.Translations.Services;
using Grove.Deployment;
using Grove.Display.Liquid;
using Grove.Data.Localization.Data;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Data.Localization.Translations;

/// <summary>
/// Registers the data localization feature.
/// </summary>
[Feature("Grove.Data.Localization.Translations")]
public sealed class Startup : StartupBase
{
    public override int ConfigureOrder => -100;

    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<DataLocalizationFilter>("d");

        services.AddScoped<TranslationsManager>();
        services.AddScoped<ITranslationsManager>(sp => sp.GetRequiredService<TranslationsManager>());
        services.AddScoped<Grove.Data.Localization.IClientStringOverrides, ClientStringOverrides>();
        services.AddApiContributor<Api.TranslationsApi>();
        services.AddScoped<Grove.Contents.AdminMenu.IAdminMenuEventHandler, AdminMenuTranslationCleanup>();
        services.AddScoped<Grove.Contents.Types.Events.IContentDefinitionEventHandler, ContentTypeTranslationCleanup>();
        services.AddRecipeExecutionStep<TranslationsStep>();

        services.AddDeployment<TranslationsDeploymentSource, TranslationsDeploymentStep>();
        services.AddDeployment<AllDataTranslationsDeploymentSource, AllDataTranslationsDeploymentStep>();

        services.AddScoped<IPermissionProvider, Permissions>();

        services.AddDataLocalization();
        services.AddSingleton<IDataTranslationProvider, DataTranslationProvider>();
    }
}
