using Fluid;
using Fluid.Values;
using Grove.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.AdminMenu;
using Grove.Contents.Management;
using Grove.Contents.Management.Display;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Routing;
using Grove.Contents.AdminNodes;
using Grove.Contents.AuditTrail.Settings;
using Grove.Contents;
using Grove.Contents.Deployment;
using Grove.Contents.Feeds.Builders;
using Grove.Contents.Handlers;
using Grove.Contents.Indexing;
using Grove.Contents.Liquid;
using Grove.Contents.Models;
using Grove.Contents.Recipes;
using Grove.Contents.Security;
using Grove.Contents.Services;
using Grove.Contents.Sitemaps;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management;
using Grove.Display.Management.Handlers;
using Grove.Display.Management.Views;
using Grove.Data.Feeds;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Data.Localization.Data;
using Grove.Modules;
using Grove.Utilities;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;
using Grove.Seo.Sitemaps.Builders;
using Grove.Seo.Sitemaps.Handlers;
using Grove.Seo.Sitemaps.Models;
using Grove.Seo.Sitemaps.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Grove.Contents;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The content items API (docs/MVC/api.md § Placement) and the content groups it filters by
        // (their definitions are Grove.Contents.Types'; the groups' pages list items).
        services.AddApiContributor<Api.ContentItemsApi>();
        services.AddApiExceptionMapper<Api.ContentSaveRefusedExceptionMapper>();
        services.AddApiExceptionMapper<Api.ContentLifecycleRefusedExceptionMapper>();
        services.AddScoped<Grove.ContentGroups.ContentGroupService>();
        services.AddScoped<Grove.ContentGroups.IContentGroupEntryResolver, Grove.ContentGroups.ContentTypeGroupEntryResolver>();
        services.AddDynamicPageSource<Grove.ContentGroups.ContentGroupPages>();


        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ContentItem>();
            o.MemberAccessStrategy.Register<ContentElement>();
            o.MemberAccessStrategy.Register<ShapeViewModel<ContentItem>>();
            o.MemberAccessStrategy.Register<ContentTypeFieldSetDefinition>();
            o.MemberAccessStrategy.Register<ContentFieldDefinition>();
            o.MemberAccessStrategy.Register<ContentFieldTypeDefinition>();
            o.MemberAccessStrategy.Register<ContentFieldSetDefinition>();

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
        services.AddDynamicPageSource<NewContentPages>();
        services.AddScoped<IContentHandler, ContentsHandler>();
        services.AddRecipeExecutionStep<ContentStep>();

        services.AddScoped<IDocumentIndexHandler, FullTextContentIndexHandler>();
        services.AddScoped<IDocumentIndexHandler, AspectsContentIndexHandler>();
        services.AddScoped<IDocumentIndexHandler, DefaultContentIndexHandler>();
        services.AddScoped<IContentHandleProvider, ContentItemIdHandleProvider>();
        services.AddScoped<IDocumentIndexHandler, ContentItemIndexCoordinator>();

        services.AddDataMigration<Migrations>();

        // Embedded content items (docs/content-engine.md › Embedded content items): stable ids,
        // the accessor, validation and snapshots; the embedded-item index; the parent's search
        // document; the display.
        services.AddScoped<IContentHandler, Embedding.EmbeddedContentHandler>();
        services.AddScopedIndexProvider<Embedding.EmbeddedContentItemIndexProvider>();
        services.AddScoped<IDocumentIndexHandler, Embedding.EmbeddedContentSearchIndexHandler>();
        services.AddScoped<IContentDisplayDriver, Embedding.EmbeddedContentDisplayDriver>();
        services.AddScoped<Grove.Services.IOptionSourceProvider, Embedding.EmbeddedContentOptionSourceProvider>();
        services.AddScoped<IEmbeddedContentItemResolver, Embedding.EmbeddedContentItemResolver>();

        // Common Part: its editors are the editor tree's (the creation date and the owner).
        services.AddContentFieldSet<CommonFieldSet>();

        // FullTextAspect
        services.AddScoped<IContentHandler, FullTextAspectContentHandler>();

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

        services.AddScoped(typeof(IContentItemRecursionHelper<>), typeof(ContentItemRecursionHelper<>));
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Generation only: the site shell's content route serves "Contents/ContentItems/{id}" on the
        // display tree (docs/blazor-display.md rework step 3); links to items without an autoroute
        // are still generated from this route's values.
        // The route values below are the item's display route values (ContentItemMetadata), so the
        // address scheme finds this endpoint by them; the constraint keeps it out of matching.
        var displayValues = new { area = "Grove.Contents", controller = "Item", action = "Display" };
        var pattern = RoutePatternFactory.Parse(
            "Contents/ContentItems/{contentItemId}",
            defaults: displayValues,
            parameterPolicies: new { contentItemId = new Grove.Routing.GenerationOnlyRouteConstraint() },
            requiredValues: displayValues);
        routes.Map(pattern, context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            })
            .WithMetadata(new RouteNameMetadata("DisplayContentItem"));
    }
}

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllContentDeploymentSource, AllContentDeploymentStep>();
        services.AddDeployment<ContentDeploymentSource, ContentDeploymentStep>();
        services.AddSiteSettingsPropertyDeploymentStep<ContentAuditTrailSettings>();
    }
}

[RequireFeatures("Grove.Contents.AdminMenu")]
public sealed class AdminMenuStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAdminNode<ContentTypesAdminNode, ContentTypesAdminNodeNavigationBuilder>();
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

[RequireFeatures("Grove.Seo.Sitemaps")]
public sealed class SitemapsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapSourceBuilder, ContentTypesSitemapSourceBuilder>();
        services.AddScoped<ISitemapSourceUpdateHandler, ContentTypesSitemapSourceUpdateHandler>();
        services.AddScoped<ISitemapSourceModifiedDateProvider, ContentTypesSitemapSourceModifiedDateProvider>();
        services.AddScoped<ISitemapSourceFactory, SitemapSourceFactory<ContentTypesSitemapSource>>();
        services.AddScoped<IContentItemsQueryProvider, DefaultContentItemsQueryProvider>();
        services.AddScoped<IContentHandler, ContentTypesSitemapUpdateHandler>();
    }
}

[RequireFeatures("Grove.Data.Feeds")]
public sealed class FeedsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Feeds
        services.AddScoped<IFeedItemBuilder, CommonFeedItemBuilder>();
    }
}

[RequireFeatures("Grove.Data.Localization.Translations")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, ContentTypeDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, ContentFieldDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, ContentDefinitionLabelsDataLocalizationProvider>();
    }
}

// ContentTypesAdminNodeDataLocalizationProvider depends on IAdminMenuAccessor, which is only
// registered when Grove.Contents.AdminMenu is enabled (see AdminMenu/Startup.cs) - without this
// separate feature-gated registration, enabling Grove.Data.Localization.Translations alone throws an
// unresolved-service exception on every /Admin/DataLocalization/Index request.
[RequireFeatures("Grove.Data.Localization.Translations", "Grove.Contents.AdminMenu")]
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
[RequireFeatures("Grove.Access.Sharing")]
public sealed class ContentSharingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Contents.Sharing.IContentShareLookup, Grove.Contents.Sharing.ContentShareLookup>();
        services.AddScoped<Grove.Contents.Sharing.ContentTypeShareableKind>();
        services.AddScoped<Grove.Contents.Sharing.ContentShareableKind>();
        services.AddScoped<Grove.Access.Sharing.IShareableKind>(provider => provider.GetRequiredService<Grove.Contents.Sharing.ContentTypeShareableKind>());
        services.AddScoped<Grove.Access.Sharing.IShareableKind>(provider => provider.GetRequiredService<Grove.Contents.Sharing.ContentShareableKind>());
        services.AddScoped<IContentHandler, Grove.Contents.Sharing.ContentSharingHandler>();
        services.AddScoped<Grove.Contents.Types.Events.IContentDefinitionEventHandler, Grove.Contents.Sharing.ContentTypeSharingHandler>();
        services.AddScoped<Grove.Environment.Shell.IFeatureEventHandler, Grove.Contents.Sharing.DefaultViewGrantsRemover>();
    }
}
