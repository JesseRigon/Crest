using Grove.Workflows.Extensions;
using Fluid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Contents.Localization.Indexing;
using Grove.Contents.Localization.Liquid;
using Grove.Contents.Localization.Security;
using Grove.Contents.Localization.Services;
using Grove.Contents.Localization.Sitemaps;
using Grove.Contents.Services;
using Grove.Environment.Shell.Configuration;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Seo.Sitemaps.Builders;

namespace Grove.Contents.Localization;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<ContentLocalizationFilter>("localization_set");
        services.AddApiContributor<Api.ContentLocalizationApi>();

        services.AddScoped<IContentFieldSetIndexHandler, LocalizationFieldSetIndexHandler>();
        services.AddSingleton<ILocalizationEntries, LocalizationEntries>();
        services.AddContentLocalization();

        services.AddPermissionProvider<Permissions>();
        services.AddScoped<Grove.Access.IResourcePermissionMapper, LocalizeContentPermissionMapper>();

        services.AddScoped<IContentItemListFilter, LocalizationFieldSetContentItemListFilter>();
    }
}

[Feature("Grove.Contents.Localization.ContentCulturePicker")]
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
        services.AddApiContributor<Api.CultureRedirectApi>();
        services.AddLiquidFilter<SwitchCultureUrlFilter>("switch_culture_url");

        services.AddSettingsGroup<Models.ContentRequestCultureProviderSettings>(Models.ContentRequestCultureProviderSettings.GroupId, "Content Culture", ContentLocalizationPermissions.ManageContentCulturePicker,
            group => group.InMenu("settings-localization", entryId: "contentrequestcultureprovider"));
        services.AddSettingsGroup<Models.ContentCulturePickerSettings>(Models.ContentCulturePickerSettings.GroupId, "Content Culture Picker", ContentLocalizationPermissions.ManageContentCulturePicker,
            group => group.InMenu("settings-localization", entryId: "contentculturepicker"));
        services.AddScoped<IContentCulturePickerService, ContentCulturePickerService>();

        services.Configure<RequestLocalizationOptions>(options => options.AddInitialRequestCultureProvider(new ContentRequestCultureProvider()));
        services.Configure<CulturePickerOptions>(_shellConfiguration.GetSection("Grove_ContentLocalization_CulturePickerOptions"));
    }
}

[Feature("Grove.Contents.Localization.Sitemaps")]
public sealed class SitemapsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapContentItemExtendedMetadataProvider, SitemapUrlHrefLangExtendedMetadataProvider>();
        services.Replace(ServiceDescriptor.Scoped<IContentItemsQueryProvider, LocalizedContentItemsQueryProvider>());
    }
}

/// <summary>The localization's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.LocalizationWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.LocalizationWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.LocalizationWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
