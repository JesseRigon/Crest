using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Routing;
using Grove.Security.Permissions;
using Grove.Seo;
using Grove.Sitemaps.Builders;
using Grove.Sitemaps.Cache;
using Grove.Sitemaps.Deployment;
using Grove.Sitemaps.Drivers;
using Grove.Sitemaps.Handlers;
using Grove.Sitemaps.Models;
using Grove.Sitemaps.Recipes;
using Grove.Sitemaps.Routing;
using Grove.Sitemaps.Services;

namespace Grove.Sitemaps;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddPageSource<AdminPages>();
        services.AddPermissionProvider<Permissions>();

        services.Configure<SitemapsOptions>(options =>
        {
            if (options.GlobalRouteValues.Count == 0)
            {
                options.GlobalRouteValues = new RouteValueDictionary
                {
                    {"Area", "Grove.Sitemaps"},
                    {"Controller", "Sitemap"},
                    {"Action", "Index"},
                };

                options.SitemapIdKey = "sitemapId";
            }
        });

        services.AddSingleton<SitemapEntries>();
        services.AddSingleton<ISitemapManager, SitemapManager>();
        services.AddSingleton<IShellRouteValuesAddressScheme, SitemapValuesAddressScheme>();
        services.AddSingleton<SitemapRouteTransformer>();

        services.AddScoped<ISitemapIdGenerator, SitemapIdGenerator>();
        services.AddScoped<ISitemapHelperService, SitemapHelperService>();
        services.AddScoped<ISitemapBuilder, DefaultSitemapBuilder>();
        services.AddScoped<ISitemapTypeBuilder, SitemapTypeBuilder>();
        services.AddScoped<ISitemapCacheProvider, DefaultSitemapCacheProvider>();
        services.AddScoped<ISitemapUpdateHandler, DefaultSitemapUpdateHandler>();
        services.AddScoped<ISitemapTypeUpdateHandler, SitemapTypeUpdateHandler>();
        services.AddScoped<ISitemapTypeBuilder, SitemapIndexTypeBuilder>();
        services.AddScoped<ISitemapTypeUpdateHandler, SitemapIndexTypeUpdateHandler>();
        services.AddScoped<ISitemapModifiedDateProvider, DefaultSitemapModifiedDateProvider>();
        services.AddScoped<IRouteableContentTypeCoordinator, DefaultRouteableContentTypeCoordinator>();

        // Sitemap Part.
        services.AddContentPart<SitemapPart>()
            .UseDisplayDriver<SitemapPartDisplayDriver>()
            .AddHandler<SitemapPartHandler>();

        // Custom sitemap path.
        services.AddScoped<ISitemapSourceBuilder, CustomPathSitemapSourceBuilder>();
        services.AddScoped<ISitemapSourceUpdateHandler, CustomPathSitemapSourceUpdateHandler>();
        services.AddScoped<ISitemapSourceModifiedDateProvider, CustomPathSitemapSourceModifiedDateProvider>();
        services.AddDisplayDriver<SitemapSource, CustomPathSitemapSourceDriver>();
        services.AddScoped<ISitemapSourceFactory, SitemapSourceFactory<CustomPathSitemapSource>>();

        services.AddRecipeExecutionStep<SitemapsStep>();

        // Allows to serialize 'SitemapType' derived types.
        services.AddJsonDerivedTypeInfo<Sitemap, SitemapType>();
        services.AddJsonDerivedTypeInfo<SitemapIndex, SitemapType>();

        // Allows to serialize 'SitemapSource' derived types.
        services.AddJsonDerivedTypeInfo<ContentTypesSitemapSource, SitemapSource>();
        services.AddJsonDerivedTypeInfo<CustomPathSitemapSource, SitemapSource>();
        services.AddJsonDerivedTypeInfo<SitemapIndexSource, SitemapSource>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapDynamicControllerRoute<SitemapRouteTransformer>("/{**sitemap}");
    }
}

[Feature("Grove.Sitemaps.RazorPages")]
public sealed class SitemapsRazorPagesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<SitemapsRazorPagesOptions>();
        services.AddScoped<IRouteableContentTypeProvider, RazorPagesContentTypeProvider>();
    }
}

[Feature("Grove.Sitemaps.Cleanup")]
public sealed class SitemapsCleanupStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTask, SitemapCacheBackgroundTask>();
    }
}

[RequireFeatures("Grove.Deployment", "Grove.Sitemaps")]
public sealed class SitemapsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllSitemapsDeploymentSource, AllSitemapsDeploymentStep, AllSitemapsDeploymentStepDriver>();
    }
}

[RequireFeatures("Grove.Seo")]
public sealed class SeoStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IRobotsProvider, SitemapsRobotsProvider>();
        services.AddSiteDisplayDriver<SitemapsRobotsSettingsDisplayDriver>();
    }
}
