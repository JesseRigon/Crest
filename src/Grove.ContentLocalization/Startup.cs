using Fluid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.ContentLocalization.Indexing;
using Grove.ContentLocalization.Liquid;
using Grove.ContentLocalization.Security;
using Grove.ContentLocalization.Services;
using Grove.ContentLocalization.Sitemaps;
using Grove.Contents.Services;
using Grove.Environment.Shell.Configuration;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;
using Grove.Sitemaps.Builders;

namespace Grove.ContentLocalization;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<ContentLocalizationFilter>("localization_set");
        services.AddApiContributor<Api.ContentLocalizationApi>();

        services.AddScoped<IContentPartIndexHandler, LocalizationPartIndexHandler>();
        services.AddSingleton<ILocalizationEntries, LocalizationEntries>();
        services.AddContentLocalization();

        services.AddPermissionProvider<Permissions>();
        services.AddScoped<Grove.Access.IResourcePermissionMapper, LocalizeContentPermissionMapper>();

        services.AddScoped<IContentItemListFilter, LocalizationPartContentItemListFilter>();
    }
}

[Feature("Grove.ContentLocalization.ContentCulturePicker")]
public sealed class ContentPickerStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public ContentPickerStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddApiContributor<Api.CulturePickerApi>();
        services.AddLiquidFilter<SwitchCultureUrlFilter>("switch_culture_url");

        services.AddSettingsGroup<Models.ContentRequestCultureProviderSettings>(Models.ContentRequestCultureProviderSettings.GroupId, "Content Culture", ContentLocalizationPermissions.ManageContentCulturePicker,
            group => group.InMenu("settings-localization", entryId: "contentrequestcultureprovider"));
        services.AddSettingsGroup<Models.ContentCulturePickerSettings>(Models.ContentCulturePickerSettings.GroupId, "Content Culture Picker", ContentLocalizationPermissions.ManageContentCulturePicker,
            group => group.InMenu("settings-localization", entryId: "contentculturepicker"));
        services.AddScoped<IContentCulturePickerService, ContentCulturePickerService>();

        services.Configure<RequestLocalizationOptions>(options => options.AddInitialRequestCultureProvider(new ContentRequestCultureProvider()));
        services.Configure<CulturePickerOptions>(_shellConfiguration.GetSection("Grove_ContentLocalization_CulturePickerOptions"));
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
           name: "RedirectToLocalizedContent",
           areaName: "Grove.ContentLocalization",
           pattern: "RedirectToLocalizedContent",
           defaults: new { controller = "ContentCulturePicker", action = "RedirectToLocalizedContent" }
       );
    }
}

[Feature("Grove.ContentLocalization.Sitemaps")]
public sealed class SitemapsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapContentItemExtendedMetadataProvider, SitemapUrlHrefLangExtendedMetadataProvider>();
        services.Replace(ServiceDescriptor.Scoped<IContentItemsQueryProvider, LocalizedContentItemsQueryProvider>());
    }
}
