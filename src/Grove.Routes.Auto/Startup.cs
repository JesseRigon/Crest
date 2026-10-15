using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Routes.Auto.Indexes;
using Grove.Routes.Auto.Services;
using Grove.Routes.Auto.Handlers;
using Grove.Routes.Auto.Indexing;
using Grove.Routes.Auto.Models;
using Grove.Routes.Auto.Routing;
using Grove.Routes.Auto.Services;
using Grove.Routes.Auto.Sitemaps;
using Grove.Routes.Auto.ViewModels;
using Grove.Contents.Localization.Handlers;
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

namespace Grove.Routes.Auto;

public sealed class Startup : StartupBase
{
    public override int ConfigureOrder
        => PlatformConstants.ConfigureOrder.Autoroute;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AutoroutePartViewModel>();

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

        // Autoroute Part
        services.AddContentPart<AutoroutePart>()
            .AddHandler<AutoroutePartHandler>();

        services.AddScoped<IContentHandler, DefaultRouteContentHandler>();
        services.AddScoped<IContentHandler, AutorouteContentHandler>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IContentPartIndexHandler, AutoroutePartIndexHandler>();

        services.AddScoped<AutoroutePartIndexProvider>();
        services.AddScoped<IScopedIndexProvider>(sp => sp.GetRequiredService<AutoroutePartIndexProvider>());
        services.AddScoped<IContentHandler>(sp => sp.GetRequiredService<AutoroutePartIndexProvider>());

        services.AddDataMigration<Migrations>();
        services.AddSingleton<IAutorouteEntries, AutorouteEntries>();
        services.AddScoped<IContentHandleProvider, AutorouteHandleProvider>();

        services.Configure<GraphQLContentOptions>(options =>
        {
            options.ConfigurePart<AutoroutePart>(partOptions =>
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

[RequireFeatures("Grove.Contents.Localization")]
public class ContentLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentLocalizationHandler, AutorouteContentLocalizationHandler>();
    }
}
