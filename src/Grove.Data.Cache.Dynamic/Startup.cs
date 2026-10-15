using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Implementation;
using Grove.Data.Cache.Dynamic.Services;
using Grove.Environment.Cache;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;

namespace Grove.Data.Cache.Dynamic;

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


        services.AddSingleton<IDynamicCache, DefaultDynamicCache>();

        services.AddTransient<IConfigureOptions<CacheOptions>, CacheOptionsConfiguration>();
        services.Configure<DynamicCacheOptions>(_shellConfiguration.GetSection("Grove_DynamicCache"));
    }
}
