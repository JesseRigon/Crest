using Grove.Workflows.Extensions;
using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.AdminMenu;
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
using Grove.Contents.Hierarchy.AdminNodes;
using Grove.Contents.Hierarchy.Drivers;
using Grove.Contents.Hierarchy.Feeds;
using Grove.Contents.Hierarchy.Indexes;
using Grove.Contents.Hierarchy.Liquid;
using Grove.Contents.Hierarchy.Models;
using Grove.Contents.Hierarchy.Services;
using Grove.Contents.Hierarchy.ViewModels;
using Grove.Data.Localization.Data;
using Grove.Modules;

namespace Grove.Contents.Hierarchy;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ChildContentItemsFieldSetViewModel>();
        })
        .AddLiquidFilter<ListCountFilter>("list_count")
        .AddLiquidFilter<ListItemsFilter>("list_items")
        .AddLiquidFilter<ContainerFilter>("container");

        services.AddIndexProvider<ParentContentItemIndexProvider>();
        services.AddApiContributor<Api.HierarchyApi>();
        // The ParentContentItem field set, welded on the items lists hold: its data is its fields; its
        // behaviour, with the lists' and their translations', the system flows below.
        services.AddContentFieldSet<ParentContentItemFieldSet>();
        services.AddScoped<HierarchyBehaviour>();

        // List Part
        services.AddContentFieldSet<ChildContentItemsFieldSet>()
            .UseDisplayDriver<ChildContentItemsFieldSetDisplayDriver>();

        services.AddDataMigration<Migrations>();
        services.AddScoped<IDocumentIndexHandler, ParentContentItemFieldSetContentIndexHandler>();
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

[RequireFeatures("Grove.Data.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Feeds
        services.AddScoped<IFeedQueryProvider, ListFeedQuery>();

        services.AddContentFieldSet<ChildContentItemsFieldSet>()
            .UseDisplayDriver<ChildContentItemsFieldSetFeedDisplayDriver>();
        services.AddContentFieldSetProjection<Projections.ListFeedProjection>();

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

/// <summary>The lists' system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.HierarchyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.HierarchyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.HierarchyWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowWeldedFieldSetSource>(provider => provider.GetRequiredService<HierarchyBehaviour>());
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
