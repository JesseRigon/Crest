using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Localization.Data;
using Grove.Modules;
using Grove.Navigation;
using Grove.Search.Deployment;
using Grove.Search.Drivers;
using Grove.Search.Migrations;
using Grove.Search.Models;
using Grove.Search.Services;
using Grove.Search.ViewModels;
using Grove.Security.Permissions;

namespace Grove.Search;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddPageSource<AdminPages>();
        services.AddSiteDisplayDriver<SearchSettingsDisplayDriver>();
    }
}

[RequireFeatures("Grove.Contents")]
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

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SearchSettingsDeploymentSource, SearchSettingsDeploymentStep, SearchSettingsDeploymentStepDriver>();
    }
}

[RequireFeatures("Grove.Liquid")]
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

[RequireFeatures("Grove.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, SearchLocalizationDataProvider>();
    }
}
