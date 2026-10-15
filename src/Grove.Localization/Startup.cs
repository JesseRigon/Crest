using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Admin.Models;
using Grove.DisplayManagement.Handlers;
using Grove.Liquid;
using Grove.Localization.Drivers;
using Grove.Localization.Liquid.Filters;
using Grove.Localization.Models;
using Grove.Localization.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;

namespace Grove.Localization;

/// <summary>
/// Represents a localization module entry point.
/// </summary>
public sealed class Startup : StartupBase
{
    public override int ConfigureOrder => -100;

    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.LocalizationApi>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<IJSLocalizer, LocalizationJSLocalizer>();

        services.AddPortableObjectLocalization(options => options.ResourcesPath = "Localization").
            AddDataAnnotationsPortableObjectLocalization();

        services.Replace(ServiceDescriptor.Singleton<ILocalizationFileLocationProvider, ModularPoFileLocationProvider>());

        services.AddLiquidFilter<TransliterateFilter>("transliterate");
    }

    /// <inheritdocs />
    public override async ValueTask ConfigureAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var localizationService = serviceProvider.GetService<ILocalizationService>();

        var defaultCulture = await localizationService.GetDefaultCultureAsync();
        var supportedCultures = await localizationService.GetSupportedCulturesAsync();

        var cultureOptions = serviceProvider.GetService<IOptions<CultureOptions>>().Value;
        var localizationOptions = serviceProvider.GetService<IOptions<RequestLocalizationOptions>>().Value;

        localizationOptions.CultureInfoUseUserOverride = !cultureOptions.IgnoreSystemSettings;
        localizationOptions.FallBackToParentUICultures = localizationOptions.FallBackToParentCultures = localizationService.FallBackToParentCultures;
        localizationOptions
            .SetDefaultCulture(defaultCulture)
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        app.UseRequestLocalization(localizationOptions);
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class LocalizationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<LocalizationSettings>();
    }
}

[Feature("Grove.Localization.ContentLanguageHeader")]
public sealed class ContentLanguageHeaderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options => options.ApplyCurrentCultureToResponseHeaders = true);
    }
}

[Feature("Grove.Localization.AdminCulturePicker")]
public sealed class CulturePickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Navbar, AdminCulturePickerNavbarDisplayDriver>();

        services.AddTransient<IConfigureOptions<RequestLocalizationOptions>, RequestLocalizationOptionsConfigurations>();
    }
}
