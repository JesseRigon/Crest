using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Routes.Auto.Indexes;
using Grove.Routes.Auto.Indexing;
using Grove.Routes.Auto.Models;
using Grove.Routes.Auto.Routing;
using Grove.Routes.Auto.Services;
using Grove.Routes.Auto.Sitemaps;
using Grove.Routes.Auto.ViewModels;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.GraphQL.Options;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Routing;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Modules;
using Grove.Routing;
using Grove.Security.Permissions;
using Grove.Seo.Sitemaps.Services;
using Grove.Workflows.Extensions;

namespace Grove.Routes.Auto;

public sealed class Startup : StartupBase
{
    public override int ConfigureOrder
        => PlatformConstants.ConfigureOrder.Autoroute;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AutorouteFieldSetViewModel>();

            o.MemberAccessStrategy.Register<LiquidContentAccessor, LiquidPropertyAccessor>("Slug", (obj, context) =>
            {
                var liquidTemplateContext = (LiquidTemplateContext)context;

                return new LiquidPropertyAccessor(liquidTemplateContext, async (slug, context) =>
                {
                    var autorouteEntries = context.Services.GetRequiredService<IAutorouteEntries>();
                    var contentManager = context.Services.GetRequiredService<IContentManager>();

                    if (!slug.StartsWith('/'))
                    {
                        slug = '/' + slug;
                    }

                    (var found, var entry) = await autorouteEntries.TryGetEntryByPathAsync(slug);

                    if (found)
                    {
                        return FluidValue.Create(await contentManager.GetAsync(entry.ContentItemId, entry.JsonPath), context.Options);
                    }

                    return NilValue.Instance;
                });
            });
        });

        // The Autoroute field set: its data is its fields; its behaviour, the system flows below;
        // its route values, read projections.
        services.AddContentFieldSet<AutorouteFieldSet>();
        services.AddScoped<AutorouteBehaviour>();
        services.AddContentFieldSetProjection<Projections.AutorouteRouteProjection>();
        services.AddContentProjection<Projections.DefaultRouteProjection>();
        services.AddContentProjection<Projections.ContainedRouteValuesProjection>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IContentFieldSetIndexHandler, AutorouteFieldSetIndexHandler>();

        services.AddScoped<AutorouteIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<AutorouteIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<AutorouteIndexProvider>());

        services.AddDataMigration<Migrations>();
        services.AddSingleton<IAutorouteEntries, AutorouteEntries>();
        services.AddScoped<IContentHandleProvider, AutorouteHandleProvider>();

        services.Configure<GraphQLContentOptions>(options =>
        {
            options.ConfigureFieldSet<AutorouteFieldSet>(partOptions =>
            {
                partOptions.Collapse = true;
            });
        });

        services.AddSingleton<IShellRouteValuesAddressScheme, AutorouteValuesAddressScheme>();
    }

    // No MVC route for autoroute paths: the site shell's content route serves them on the display
    // tree (docs/blazor-display.md rework step 3); the address scheme above still generates them.
}

[RequireFeatures("Grove.Seo.Sitemaps")]
public class SitemapStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRouteableContentTypeProvider, AutorouteContentTypeProvider>();
    }
}

[RequireFeatures("Grove.Contents")]
public sealed class ContentAutourouteStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddShapeTableProvider<ContentAutorouteShapeTableProvider>();
    }
}

/// <summary>The permalink's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.AutorouteWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.AutorouteWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.AutorouteWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
