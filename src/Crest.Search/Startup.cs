using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Localization.Data;
using Crest.Modules;
using Crest.Navigation;
using Crest.Search.Deployment;
using Crest.Search.Drivers;
using Crest.Search.Migrations;
using Crest.Search.Models;
using Crest.Search.Services;
using Crest.Search.ViewModels;
using Crest.Security.Permissions;

namespace Crest.Search;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddSiteDisplayDriver<SearchSettingsDisplayDriver>();
    }
}

[RequireFeatures("Crest.Contents")]
public sealed class ContentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SearchMigrations>();

        services
            .AddContentPart<SearchFormPart>()
            .UseDisplayDriver<SearchFormPartDisplayDriver>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SearchSettingsDeploymentSource, SearchSettingsDeploymentStep, SearchSettingsDeploymentStepDriver>();
    }
}

[RequireFeatures("Crest.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<SearchIndexViewModel>();
            o.MemberAccessStrategy.Register<SearchFormViewModel>();
            o.MemberAccessStrategy.Register<SearchResultsViewModel>();
        });
    }
}

[RequireFeatures("Crest.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, SearchLocalizationDataProvider>();
    }
}
