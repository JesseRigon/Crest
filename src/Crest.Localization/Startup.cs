using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Crest.Admin.Models;
using Crest.DisplayManagement.Handlers;
using Crest.Liquid;
using Crest.Localization.Drivers;
using Crest.Localization.Liquid.Filters;
using Crest.Localization.Models;
using Crest.Localization.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;

namespace Crest.Localization;

/// <summary>
/// Represents a localization module entry point.
/// </summary>
public sealed class Startup : StartupBase
{
    public override int ConfigureOrder => -100;

    /// <inheritdocs />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<LocalizationSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
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

[RequireFeatures("Crest.Deployment")]
public sealed class LocalizationDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<LocalizationSettings, LocalizationDeploymentStartup>(S => S["Culture settings"], S => S["Exports the culture settings."]);
    }
}

[Feature("Crest.Localization.ContentLanguageHeader")]
public sealed class ContentLanguageHeaderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options => options.ApplyCurrentCultureToResponseHeaders = true);
    }
}

[Feature("Crest.Localization.AdminCulturePicker")]
public sealed class CulturePickerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Navbar, AdminCulturePickerNavbarDisplayDriver>();

        services.AddTransient<IConfigureOptions<RequestLocalizationOptions>, RequestLocalizationOptionsConfigurations>();
    }
}
