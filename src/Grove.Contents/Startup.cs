using Fluid;
using Fluid.Values;
using Grove.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.AdminMenu;
using Grove.ContentManagement;
using Grove.ContentManagement.Display;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;
using Grove.ContentManagement.Routing;
using Grove.Contents.AdminNodes;
using Grove.Contents.AuditTrail.Settings;
using Grove.Contents.Controllers;
using Grove.Contents;
using Grove.Contents.Deployment;
using Grove.Contents.Drivers;
using Grove.Contents.Endpoints.Api;
using Grove.Contents.Feeds.Builders;
using Grove.Contents.Handlers;
using Grove.Contents.Indexing;
using Grove.Contents.Liquid;
using Grove.Contents.Models;
using Grove.Contents.Recipes;
using Grove.Contents.Security;
using Grove.Contents.Services;
using Grove.Contents.Sitemaps;
using Grove.Contents.TagHelpers;
using Grove.Contents.ViewModels;
using Grove.ContentTypes.Editors;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Liquid;
using Grove.DisplayManagement.Liquid.Tags;
using Grove.DisplayManagement.Views;
using Grove.Feeds;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Localization.Data;
using Grove.Modules;
using Grove.Mvc.Utilities;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;
using Grove.Settings.Deployment;
using Grove.Sitemaps.Builders;
using Grove.Sitemaps.Handlers;
using Grove.Sitemaps.Models;
using Grove.Sitemaps.Services;
using YesSql.Filters.Query;

namespace Grove.Contents;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentServices();
        services.AddSingleton<IAnchorTag, ContentAnchorTag>();

        // The content items API (docs/MVC/api.md § Placement) and the content groups it filters by
        // (their definitions are Grove.ContentTypes'; the groups' pages list items).
        services.AddApiContributor<Api.ContentItemsApi>();
        services.AddScoped<Grove.ContentGroups.ContentGroupService>();
        services.AddScoped<Grove.ContentGroups.IContentGroupEntryResolver, Grove.ContentGroups.ContentTypeGroupEntryResolver>();
        services.AddDynamicPageSource<Grove.ContentGroups.ContentGroupPages>();

        services.Configure<LiquidViewOptions>(o =>
        {
            o.LiquidViewParserConfiguration.Add(parser => parser.RegisterParserTag("contentitem", parser.ArgumentsListParser, ContentItemTag.WriteToAsync));
        });

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ContentItem>();
            o.MemberAccessStrategy.Register<ContentElement>();
            o.MemberAccessStrategy.Register<ShapeViewModel<ContentItem>>();
            o.MemberAccessStrategy.Register<ContentTypePartDefinition>();
            o.MemberAccessStrategy.Register<ContentPartFieldDefinition>();
            o.MemberAccessStrategy.Register<ContentFieldDefinition>();
            o.MemberAccessStrategy.Register<ContentPartDefinition>();

            o.Filters.AddFilter("display_text", DisplayTextFilter.DisplayText);

            o.Scope.SetValue("Content", new ObjectValue(new LiquidContentAccessor()));
            o.MemberAccessStrategy.Register<LiquidContentAccessor, LiquidPropertyAccessor>("ContentItemId", (obj, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;

                return new LiquidPropertyAccessor(liquidTemplateContext, async (contentItemId, context) =>
                {
                    var contentManager = context.Services.GetRequiredService<IContentManager>();

                    return FluidValue.Create(await contentManager.GetAsync(contentItemId), context.Options);
                });
            });

            o.MemberAccessStrategy.Register<LiquidContentAccessor, LiquidPropertyAccessor>("ContentItemVersionId", (obj, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;

                return new LiquidPropertyAccessor(liquidTemplateContext, async (contentItemVersionId, context) =>
                {
                    var contentManager = context.Services.GetRequiredService<IContentManager>();

                    return FluidValue.Create(await contentManager.GetVersionAsync(contentItemVersionId), context.Options);
                });
            });

            o.MemberAccessStrategy.Register<LiquidContentAccessor, LiquidPropertyAccessor>("Latest", (obj, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;

                return new LiquidPropertyAccessor(liquidTemplateContext, (name, context) =>
                {
                    return GetContentByHandleAsync(context, name, true);
                });
            });

            o.MemberAccessStrategy.Register<LiquidContentAccessor, FluidValue>((obj, name, context) => GetContentByHandleAsync((LiquidTemplateContext)context, name));

            static async Task<FluidValue> GetContentByHandleAsync(LiquidTemplateContext context, string handle, bool latest = false)
            {
                var contentHandleManager = context.Services.GetRequiredService<IContentHandleManager>();

                var contentItemId = await contentHandleManager.GetContentItemIdAsync(handle);

                if (contentItemId == null)
                {
                    return NilValue.Instance;
                }

                var contentManager = context.Services.GetRequiredService<IContentManager>();

                var contentItem = await contentManager.GetAsync(contentItemId, latest ? VersionOptions.Latest : VersionOptions.Published);
                return FluidValue.Create(contentItem, context.Options);
            }
        })
        .AddLiquidFilter<DisplayUrlFilter>("display_url")
        .AddLiquidFilter<BuildDisplayFilter>("shape_build_display")
        .AddLiquidFilter<ContentItemFilter>("content_item_id")
        .AddLiquidFilter<FullTextFilter>("full_text");

        services.AddContentManagement();
        services.AddContentManagementDisplay();
        services.AddPermissionProvider<Permissions>();
        services.AddPermissionProvider<ContentTypePermissions>();
        services.AddScoped<IResourcePermissionMapper, ContentResourcePermissionMapper>();
        services.AddScoped<IScopeProvider, ContentItemScopeProvider>();
        services.AddShapeTableProvider<Shapes>();
        services.AddShapeTableProvider<AdminDashboardShapeTableProvider>();
        services.AddDynamicPageSource<NewContentPages>();
        services.AddScoped<IContentDisplayDriver, ContentsDriver>();
        services.AddScoped<IContentHandler, ContentsHandler>();
        services.AddRecipeExecutionStep<ContentStep>();

        services.AddScoped<IDocumentIndexHandler, FullTextContentIndexHandler>();
        services.AddScoped<IDocumentIndexHandler, AspectsContentIndexHandler>();
        services.AddScoped<IDocumentIndexHandler, DefaultContentIndexHandler>();
        services.AddScoped<IContentHandleProvider, ContentItemIdHandleProvider>();
        services.AddScoped<IDocumentIndexHandler, ContentItemIndexCoordinator>();

        services.AddDataMigration<Migrations>();

        // Common Part
        services.AddContentPart<CommonPart>()
            .UseDisplayDriver<DateEditorDriver>()
            .UseDisplayDriver<OwnerEditorDriver>();


        // FullTextAspect
        services.AddScoped<IContentHandler, FullTextAspectContentHandler>();

        services.AddTagHelpers<ContentLinkTagHelper>();
        services.AddTagHelpers<ContentItemTagHelper>();
        services.Configure<AutorouteOptions>(options =>
        {
            if (options.GlobalRouteValues.Count == 0)
            {
                options.GlobalRouteValues = new RouteValueDictionary
                {
                    {"Area", "Grove.Contents"},
                    {"Controller", "Item"},
                    {"Action", "Display"},
                };

                options.ContentItemIdKey = "contentItemId";
                options.ContainedContentItemIdKey = "containedContentItemId";
                options.JsonPathKey = "jsonPath";
            }
        });

        services.AddScoped<IContentsAdminListQueryService, DefaultContentsAdminListQueryService>();

        services.AddDisplayDriver<ContentOptionsViewModel, ContentOptionsDisplayDriver>();

        services.AddScoped(typeof(IContentItemRecursionHelper<>), typeof(ContentItemRecursionHelper<>));

        services.AddSingleton<IContentsAdminListFilterParser>(sp =>
        {
            var filterProviders = sp.GetServices<IContentsAdminListFilterProvider>();
            var builder = new QueryEngineBuilder<ContentItem>();
            foreach (var provider in filterProviders)
            {
                provider.Build(builder);
            }

            var parser = builder.Build();

            return new DefaultContentsAdminListFilterParser(parser);
        });

        services.AddTransient<IContentsAdminListFilterProvider, DefaultContentsAdminListFilterProvider>();
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddGetContentEndpoint()
            .AddCreateContentEndpoint()
            .AddDeleteContentEndpoint();

        var itemControllerName = typeof(ItemController).ControllerName();

        // Generation only: the site shell's content route serves "Contents/ContentItems/{id}" on the
        // display tree (docs/blazor-display.md rework step 3); links to items without an autoroute
        // are still generated from this route's values.
        routes.MapAreaControllerRoute(
            name: "DisplayContentItem",
            areaName: "Grove.Contents",
            pattern: "Contents/ContentItems/{contentItemId}",
            defaults: new { controller = itemControllerName, action = "Display" },
            constraints: new { contentItemId = new Grove.Routing.GenerationOnlyRouteConstraint() }
        );

        routes.MapAreaControllerRoute(
            name: "PreviewContentItem",
            areaName: "Grove.Contents",
            pattern: "Contents/ContentItems/{contentItemId}/Preview",
            defaults: new { controller = itemControllerName, action = nameof(ItemController.Preview) }
        );

        routes.MapAreaControllerRoute(
            name: "PreviewContentItemVersion",
            areaName: "Grove.Contents",
            pattern: "Contents/ContentItems/{contentItemId}/Version/{version}/Preview",
            defaults: new { controller = itemControllerName, action = nameof(ItemController.Preview) }
        );
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllContentDeploymentSource, AllContentDeploymentStep, AllContentDeploymentStepDriver>();
        services.AddDeployment<ContentDeploymentSource, ContentDeploymentStep, ContentDeploymentStepDriver>();
        services.AddSiteSettingsPropertyDeploymentStep<ContentAuditTrailSettings, DeploymentStartup>(S => S["Content Audit Trail settings"], S => S["Exports the content audit trail settings."]);
    }
}

[RequireFeatures("Grove.AdminMenu")]
public sealed class AdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<ContentTypesAdminNode, ContentTypesAdminNodeNavigationBuilder, ContentTypesAdminNodeDriver>();
    }
}

[Feature("Grove.Contents.FileContentDefinition")]
public sealed class FileContentDefinitionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddFileContentDefinitionStore();
    }
}

[RequireFeatures("Grove.Sitemaps")]
public sealed class SitemapsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapSourceBuilder, ContentTypesSitemapSourceBuilder>();
        services.AddScoped<ISitemapSourceUpdateHandler, ContentTypesSitemapSourceUpdateHandler>();
        services.AddScoped<ISitemapSourceModifiedDateProvider, ContentTypesSitemapSourceModifiedDateProvider>();
        services.AddDisplayDriver<SitemapSource, ContentTypesSitemapSourceDriver>();
        services.AddScoped<ISitemapSourceFactory, SitemapSourceFactory<ContentTypesSitemapSource>>();
        services.AddScoped<IContentItemsQueryProvider, DefaultContentItemsQueryProvider>();
        services.AddScoped<IContentHandler, ContentTypesSitemapUpdateHandler>();
    }
}

[RequireFeatures("Grove.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Feeds
        services.AddScoped<IFeedItemBuilder, CommonFeedItemBuilder>();
    }
}

[RequireFeatures("Grove.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ContentTypeDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, ContentFieldDataLocalizationProvider>();
    }
}

// ContentTypesAdminNodeDataLocalizationProvider depends on IAdminMenuAccessor, which is only
// registered when Grove.AdminMenu is enabled (see AdminMenu/Startup.cs) - without this
// separate feature-gated registration, enabling Grove.DataLocalization alone throws an
// unresolved-service exception on every /Admin/DataLocalization/Index request.
[RequireFeatures("Grove.DataLocalization", "Grove.AdminMenu")]
public sealed class DataLocalizationAdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ContentTypesAdminNodeDataLocalizationProvider>();
    }
}

/// <summary>
/// Content items and content types as shareable kinds while the Sharing feature is on
/// (docs/sharing.md § 6): sharing becomes the authority for content items and their lists;
/// <see cref="ContentItemScopeProvider"/> keeps only the version rule, and the role-based content
/// mapper still answers the permissions sharing does not map. Off, nothing here is registered.
/// </summary>
[RequireFeatures("Grove.Sharing")]
public sealed class ContentSharingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Contents.Sharing.IContentShareLookup, Grove.Contents.Sharing.ContentShareLookup>();
        services.AddScoped<Grove.Contents.Sharing.ContentTypeShareableKind>();
        services.AddScoped<Grove.Contents.Sharing.ContentShareableKind>();
        services.AddScoped<Grove.Sharing.IShareableKind>(provider => provider.GetRequiredService<Grove.Contents.Sharing.ContentTypeShareableKind>());
        services.AddScoped<Grove.Sharing.IShareableKind>(provider => provider.GetRequiredService<Grove.Contents.Sharing.ContentShareableKind>());
        services.AddScoped<IContentHandler, Grove.Contents.Sharing.ContentSharingHandler>();
        services.AddScoped<Grove.ContentTypes.Events.IContentDefinitionEventHandler, Grove.Contents.Sharing.ContentTypeSharingHandler>();
        services.AddScoped<Grove.Environment.Shell.IFeatureEventHandler, Grove.Contents.Sharing.DefaultViewGrantsRemover>();
    }
}
