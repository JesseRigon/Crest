using Fluid;
using Fluid.Values;
using Crest.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Crest.AdminMenu;
using Crest.ContentManagement;
using Crest.ContentManagement.Display;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Routing;
using Crest.Contents.AdminNodes;
using Crest.Contents.AuditTrail.Settings;
using Crest.Contents.Controllers;
using Crest.Contents;
using Crest.Contents.Deployment;
using Crest.Contents.Drivers;
using Crest.Contents.Endpoints.Api;
using Crest.Contents.Feeds.Builders;
using Crest.Contents.Handlers;
using Crest.Contents.Indexing;
using Crest.Contents.Liquid;
using Crest.Contents.Models;
using Crest.Contents.Recipes;
using Crest.Contents.Security;
using Crest.Contents.Services;
using Crest.Contents.Settings;
using Crest.Contents.Sitemaps;
using Crest.Contents.TagHelpers;
using Crest.Contents.ViewModels;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Liquid;
using Crest.DisplayManagement.Liquid.Tags;
using Crest.DisplayManagement.Views;
using Crest.Feeds;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Lists.Settings;
using Crest.Localization.Data;
using Crest.Modules;
using Crest.Mvc.Utilities;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using Crest.Sitemaps.Builders;
using Crest.Sitemaps.Handlers;
using Crest.Sitemaps.Models;
using Crest.Sitemaps.Services;
using YesSql.Filters.Query;

namespace Crest.Contents;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentServices();
        services.AddSingleton<IAnchorTag, ContentAnchorTag>();

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
        services.AddNavigationProvider<AdminMenu>();
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

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, CommonPartSettingsDisplayDriver>();

        // FullTextAspect
        services.AddScoped<IContentTypeDefinitionDisplayDriver, FullTextAspectSettingsDisplayDriver>();
        services.AddScoped<IContentHandler, FullTextAspectContentHandler>();

        services.AddTagHelpers<ContentLinkTagHelper>();
        services.AddTagHelpers<ContentItemTagHelper>();
        services.Configure<AutorouteOptions>(options =>
        {
            if (options.GlobalRouteValues.Count == 0)
            {
                options.GlobalRouteValues = new RouteValueDictionary
                {
                    {"Area", "Crest.Contents"},
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

        routes.MapAreaControllerRoute(
            name: "DisplayContentItem",
            areaName: "Crest.Contents",
            pattern: "Contents/ContentItems/{contentItemId}",
            defaults: new { controller = itemControllerName, action = nameof(ItemController.Display) }
        );

        routes.MapAreaControllerRoute(
            name: "PreviewContentItem",
            areaName: "Crest.Contents",
            pattern: "Contents/ContentItems/{contentItemId}/Preview",
            defaults: new { controller = itemControllerName, action = nameof(ItemController.Preview) }
        );

        routes.MapAreaControllerRoute(
            name: "PreviewContentItemVersion",
            areaName: "Crest.Contents",
            pattern: "Contents/ContentItems/{contentItemId}/Version/{version}/Preview",
            defaults: new { controller = itemControllerName, action = nameof(ItemController.Preview) }
        );
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllContentDeploymentSource, AllContentDeploymentStep, AllContentDeploymentStepDriver>();
        services.AddDeployment<ContentDeploymentSource, ContentDeploymentStep, ContentDeploymentStepDriver>();
        services.AddSiteSettingsPropertyDeploymentStep<ContentAuditTrailSettings, DeploymentStartup>(S => S["Content Audit Trail settings"], S => S["Exports the content audit trail settings."]);
    }
}

[RequireFeatures("Crest.AdminMenu")]
public sealed class AdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<ContentTypesAdminNode, ContentTypesAdminNodeNavigationBuilder, ContentTypesAdminNodeDriver>();
    }
}

[Feature("Crest.Contents.FileContentDefinition")]
public sealed class FileContentDefinitionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddFileContentDefinitionStore();
    }
}

[RequireFeatures("Crest.Sitemaps")]
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

[RequireFeatures("Crest.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Feeds
        services.AddScoped<IFeedItemBuilder, CommonFeedItemBuilder>();
    }
}

[RequireFeatures("Crest.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ContentTypeDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, ContentFieldDataLocalizationProvider>();
    }
}

// ContentTypesAdminNodeDataLocalizationProvider depends on IAdminMenuAccessor, which is only
// registered when Crest.AdminMenu is enabled (see AdminMenu/Startup.cs) - without this
// separate feature-gated registration, enabling Crest.DataLocalization alone throws an
// unresolved-service exception on every /Admin/DataLocalization/Index request.
[RequireFeatures("Crest.DataLocalization", "Crest.AdminMenu")]
public sealed class DataLocalizationAdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ContentTypesAdminNodeDataLocalizationProvider>();
    }
}
