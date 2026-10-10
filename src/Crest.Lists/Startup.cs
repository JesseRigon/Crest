using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.AdminMenu;
using Crest.ContentLocalization.Handlers;
using Crest.ContentLocalization.Models;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Contents.Services;
using Crest.Contents.ViewModels;
using Crest.ContentTypes.Editors;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Feeds;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Lists.AdminNodes;
using Crest.Lists.Drivers;
using Crest.Lists.Feeds;
using Crest.Lists.Handlers;
using Crest.Lists.Indexes;
using Crest.Lists.Liquid;
using Crest.Lists.Models;
using Crest.Lists.Services;
using Crest.Lists.Settings;
using Crest.Lists.ViewModels;
using Crest.Localization.Data;
using Crest.Modules;

namespace Crest.Lists;

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

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ListPartSettingsDisplayDriver>();
        services.AddDataMigration<Migrations>();
        services.AddScoped<IDocumentIndexHandler, ContainedPartContentIndexHandler>();
        services.AddScoped<IContainerService, ContainerService>();
    }
}

[RequireFeatures("Crest.AdminMenu")]
public sealed class AdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<ListsAdminNode, ListsAdminNodeNavigationBuilder, ListsAdminNodeDriver>();
    }
}

[RequireFeatures("Crest.ContentLocalization")]
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

[RequireFeatures("Crest.Feeds")]
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
            areaName: "Crest.Feeds",
            pattern: "Contents/Lists/{contentItemId}/rss",
            defaults: new { controller = "Feed", action = "Index", format = "rss" }
        );
    }
}

[RequireFeatures("Crest.AdminMenu", "Crest.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ListsAdminNodeDataLocalizationProvider>();
    }
}
