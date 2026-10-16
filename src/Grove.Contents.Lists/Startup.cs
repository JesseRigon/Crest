using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.AdminMenu;
using Grove.Contents.Localization.Handlers;
using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Services;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Data.Feeds;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Contents.Lists.AdminNodes;
using Grove.Contents.Lists.Drivers;
using Grove.Contents.Lists.Feeds;
using Grove.Contents.Lists.Handlers;
using Grove.Contents.Lists.Indexes;
using Grove.Contents.Lists.Liquid;
using Grove.Contents.Lists.Models;
using Grove.Contents.Lists.Services;
using Grove.Contents.Lists.ViewModels;
using Grove.Data.Localization.Data;
using Grove.Modules;

namespace Grove.Contents.Lists;

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
        services.AddApiContributor<Api.ListsApi>();
        services.AddScoped<IContentHandler, ContainedPartHandler>();
        services.AddContentPart<ContainedPart>();

        // List Part
        services.AddContentPart<ListPart>()
            .UseDisplayDriver<ListPartDisplayDriver>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IDocumentIndexHandler, ContainedPartContentIndexHandler>();
        services.AddScoped<IContainerService, ContainerService>();
    }
}

[RequireFeatures("Grove.Contents.AdminMenu")]
public sealed class AdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<ListsAdminNode, ListsAdminNodeNavigationBuilder>();
    }
}

[RequireFeatures("Grove.Contents.Localization")]
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

[RequireFeatures("Grove.Data.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Feeds
        services.AddScoped<IFeedQueryProvider, ListFeedQuery>();

        services.AddContentPart<ListPart>()
            .UseDisplayDriver<ListPartFeedDisplayDriver>()
            .AddHandler<ListPartFeedHandler>();

        // The list's feed, served by the Feeds module.
        services.Configure<FeedRoutesOptions>(options => options.MapFeed("Contents/Lists/{contentItemId}/rss", "rss"));
    }
}

[RequireFeatures("Grove.Contents.AdminMenu", "Grove.Data.Localization.Translations")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ListsAdminNodeDataLocalizationProvider>();
    }
}
