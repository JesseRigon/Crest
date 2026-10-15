using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Implementation;
using Grove.DynamicCache.EventHandlers;
using Grove.DynamicCache.Services;
using Grove.DynamicCache.TagHelpers;
using Grove.Environment.Cache;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;

namespace Grove.DynamicCache;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDynamicCacheService, DefaultDynamicCacheService>();
        services.AddScoped<ITagRemovedEventHandler>(sp => sp.GetRequiredService<IDynamicCacheService>());

        services.AddScoped<IShapeDisplayEvents, DynamicCacheShapeDisplayEvents>();

        services.AddSingleton<IDynamicCache, DefaultDynamicCache>();
        services.AddSingleton<DynamicCacheTagHelperService>();
        services.AddTagHelpers<DynamicCacheTagHelper>();
        services.AddTagHelpers<CacheDependencyTagHelper>();

        services.AddTransient<IConfigureOptions<CacheOptions>, CacheOptionsConfiguration>();
        services.Configure<DynamicCacheOptions>(_shellConfiguration.GetSection("Grove_DynamicCache"));
    }
}
