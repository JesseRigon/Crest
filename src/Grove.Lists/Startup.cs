using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.AdminMenu;
using Grove.ContentLocalization.Handlers;
using Grove.ContentLocalization.Models;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.Contents.Services;
using Grove.Contents.ViewModels;
using Grove.ContentTypes.Editors;
using Grove.Data;
using Grove.Data.Migration;
using Grove.DisplayManagement.Handlers;
using Grove.Feeds;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Lists.AdminNodes;
using Grove.Lists.Drivers;
using Grove.Lists.Feeds;
using Grove.Lists.Handlers;
using Grove.Lists.Indexes;
using Grove.Lists.Liquid;
using Grove.Lists.Models;
using Grove.Lists.Services;
using Grove.Lists.ViewModels;
using Grove.Localization.Data;
using Grove.Modules;

namespace Grove.Lists;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ListPartViewModel>();
        })
        .AddLiquidFilter<ListCountFilter>("list_count")
        .AddLiquidFilter<ListItemsFilter>("list_items")
        .AddLiquidFilter<ContainerFilter>("container");

        services.AddIndexProvider<ContainedPartIndexProvider>();
        services.AddScoped<IContentDisplayDriver, ContainedPartDisplayDriver>();
        services.AddScoped<IContentHandler, ContainedPartHandler>();
        services.AddContentPart<ContainedPart>();
        services.AddScoped<IContentsAdminListFilter, ListPartContentsAdminListFilter>();
        services.AddDisplayDriver<ContentOptionsViewModel, ListPartContentsAdminListDisplayDriver>();

        // List Part
        services.AddContentPart<ListPart>()
            .UseDisplayDriver<ListPartDisplayDriver>()
            .AddHandler<ListPartHandler>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IDocumentIndexHandler, ContainedPartContentIndexHandler>();
        services.AddScoped<IContainerService, ContainerService>();
    }
}

[RequireFeatures("Grove.AdminMenu")]
public sealed class AdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<ListsAdminNode, ListsAdminNodeNavigationBuilder, ListsAdminNodeDriver>();
    }
}

[RequireFeatures("Grove.ContentLocalization")]
public sealed class ContentLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentLocalizationPartHandler, ContainedPartLocalizationHandler>();
        services.AddScoped<IContentLocalizationPartHandler, ListPartLocalizationHandler>();
        services.AddContentPart<LocalizationPart>()
            .AddHandler<LocalizationContainedPartHandler>();
    }
}

[RequireFeatures("Grove.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Feeds
        services.AddScoped<IFeedQueryProvider, ListFeedQuery>();

        services.AddContentPart<ListPart>()
            .UseDisplayDriver<ListPartFeedDisplayDriver>()
            .AddHandler<ListPartFeedHandler>();
    }
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "ListFeed",
            areaName: "Grove.Feeds",
            pattern: "Contents/Lists/{contentItemId}/rss",
            defaults: new { controller = "Feed", action = "Index", format = "rss" }
        );
    }
}

[RequireFeatures("Grove.AdminMenu", "Grove.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ListsAdminNodeDataLocalizationProvider>();
    }
}
