using Fluid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Admin.Models;
using Crest.ContentLocalization.Drivers;
using Crest.ContentLocalization.Indexing;
using Crest.ContentLocalization.Liquid;
using Crest.ContentLocalization.Security;
using Crest.ContentLocalization.Services;
using Crest.ContentLocalization.Sitemaps;
using Crest.ContentLocalization.ViewModels;
using Crest.Contents.Services;
using Crest.Contents.ViewModels;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell.Configuration;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Sitemaps.Builders;

namespace Crest.ContentLocalization;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<LocalizationPartViewModel>();
        })
        .AddLiquidFilter<ContentLocalizationFilter>("localization_set");

        services.AddScoped<IContentPartIndexHandler, LocalizationPartIndexHandler>();
        services.AddSingleton<ILocalizationEntries, LocalizationEntries>();
        services.AddContentLocalization();

        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IAuthorizationHandler, LocalizeContentAuthorizationHandler>();

        services.AddScoped<IContentsAdminListFilter, LocalizationPartContentsAdminListFilter>();
        services.AddTransient<IContentsAdminListFilterProvider, LocalizationPartContentsAdminListFilterProvider>();
        services.AddDisplayDriver<ContentOptionsViewModel, LocalizationContentsAdminListDisplayDriver>();
    }
}

[Feature("Crest.ContentLocalization.ContentCulturePicker")]
public sealed class ContentPickerStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public ContentPickerStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Navbar, ContentCulturePickerNavbarDisplayDriver>();
        services.AddLiquidFilter<SwitchCultureUrlFilter>("switch_culture_url");

        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IContentCulturePickerService, ContentCulturePickerService>();
        services.AddSiteDisplayDriver<ContentCulturePickerSettingsDriver>();
        services.AddSiteDisplayDriver<ContentRequestCultureProviderSettingsDriver>();

        services.Configure<RequestLocalizationOptions>(options => options.AddInitialRequestCultureProvider(new ContentRequestCultureProvider()));
        services.Configure<CulturePickerOptions>(_shellConfiguration.GetSection("Crest_ContentLocalization_CulturePickerOptions"));
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
           name: "RedirectToLocalizedContent",
           areaName: "Crest.ContentLocalization",
           pattern: "RedirectToLocalizedContent",
           defaults: new { controller = "ContentCulturePicker", action = "RedirectToLocalizedContent" }
       );
    }
}

[Feature("Crest.ContentLocalization.Sitemaps")]
public sealed class SitemapsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapContentItemExtendedMetadataProvider, SitemapUrlHrefLangExtendedMetadataProvider>();
        services.Replace(ServiceDescriptor.Scoped<IContentItemsQueryProvider, LocalizedContentItemsQueryProvider>());
    }
}
