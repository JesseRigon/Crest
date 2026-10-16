using Grove.Contents.Management.Handlers;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Routing;
using Grove.Security.Permissions;
using Grove.Seo;
using Grove.Seo.Sitemaps.Builders;
using Grove.Seo.Sitemaps.Cache;
using Grove.Seo.Sitemaps.Deployment;
using Grove.Seo.Sitemaps.Handlers;
using Grove.Seo.Sitemaps.Models;
using Grove.Seo.Sitemaps.Recipes;
using Grove.Seo.Sitemaps.Routing;
using Grove.Seo.Sitemaps.Services;

namespace Grove.Seo.Sitemaps;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<Migrations>();
        services.AddPageSource<AdminPages>();

        // The sitemaps pages (Grove.Seo.Sitemaps.BlazorWasm) and their API.
        services.AddApiContributor<Api.SitemapsApi>();

        // The sitemap documents at their own paths.
        services.AddApiContributor<Api.SitemapDocumentsApi>();
        services.Configure<RouteOptions>(options => options.ConstraintMap[SitemapPathRouteConstraint.Name] = typeof(SitemapPathRouteConstraint));
        services.AddPermissionProvider<Permissions>();

        services.Configure<SitemapsOptions>(options =>
        {
            if (options.GlobalRouteValues.Count == 0)
            {
                options.GlobalRouteValues = new RouteValueDictionary
                {
                    {"Area", "Grove.Seo.Sitemaps"},
                    {"Controller", "Sitemap"},
                    {"Action", "Index"},
                };

                options.SitemapIdKey = "sitemapId";
            }
        });

        services.AddSingleton<SitemapEntries>();
        services.AddSingleton<ISitemapManager, SitemapManager>();
        services.AddSingleton<IShellRouteValuesAddressScheme, SitemapValuesAddressScheme>();

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
        // Edited by SitemapFieldSetEditor (Grove.Display.Components) on the editor tree.
        // Its data is its fields; its entry, a read projection.
        services.AddContentFieldSet<SitemapFieldSet>();
        services.AddContentFieldSetProjection<Projections.SitemapProjection>();

        // Custom sitemap path.
        services.AddScoped<ISitemapSourceBuilder, CustomPathSitemapSourceBuilder>();
        services.AddScoped<ISitemapSourceUpdateHandler, CustomPathSitemapSourceUpdateHandler>();
        services.AddScoped<ISitemapSourceModifiedDateProvider, CustomPathSitemapSourceModifiedDateProvider>();
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
}

[Feature("Grove.Seo.Sitemaps.Cleanup")]
public sealed class SitemapsCleanupStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTask, SitemapCacheBackgroundTask>();
    }
}

[RequireFeatures("Grove.Deployment", "Grove.Seo.Sitemaps")]
public sealed class SitemapsDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllSitemapsDeploymentSource, AllSitemapsDeploymentStep>();
    }
}

[RequireFeatures("Grove.Seo")]
public sealed class SeoStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IRobotsProvider, SitemapsRobotsProvider>();
        services.AddSettingsModel<Models.SitemapsRobotsSettings>(SeoConstants.RobotsSettingsGroupId, "Sitemaps");
    }
}
