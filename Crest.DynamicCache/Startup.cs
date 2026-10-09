using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Implementation;
using Crest.DynamicCache.EventHandlers;
using Crest.DynamicCache.Services;
using Crest.DynamicCache.TagHelpers;
using Crest.Environment.Cache;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;

namespace Crest.DynamicCache;

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
        services.Configure<DynamicCacheOptions>(_shellConfiguration.GetSection("Crest_DynamicCache"));
    }
}
