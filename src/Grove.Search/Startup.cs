using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Data.Localization.Data;
using Grove.Modules;
using Grove.Search.Abstractions;
using Grove.Search.Api;
using Grove.Search.Deployment;
using Grove.Search.Models;
using Grove.Search.Services;
using Grove.Security.Permissions;

namespace Grove.Search;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddSettingsGroup<SearchSettings>(SearchConstants.SearchSettingsGroupId, "Site Search", SearchPermissions.ManageSearchSettings,
            group => group.InMenu("settings-search", entryId: "searchsettings"));

        // The search seam: the engines register their ISearchService (AddSearchService); the
        // search API and the site search page (Grove.Search.BlazorWasm) go through ISiteSearch.
        services.AddScoped<ISiteSearch, SiteSearch>();
        services.AddApiContributor<SearchApi>();
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SearchSettingsDeploymentSource, SearchSettingsDeploymentStep>();
    }
}

[RequireFeatures("Grove.Data.Localization.Translations")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, SearchLocalizationDataProvider>();
    }
}
