using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Crest.Caching.Distributed;
using Crest.Environment.Cache;
using Crest.Environment.Cache.CacheContextProviders;
using Crest.Modules;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds tenant level caching services.
    /// </summary>
    public static PlatformBuilder AddCaching(this PlatformBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISignal>(sp =>
            {
                var messageBus = sp.GetService<IMessageBus>();

                if (messageBus == null)
                {
                    return new Signal();
                }
                else
                {
                    return new DistributedSignal(messageBus);
                }
            });

            services.AddSingleton<IModularTenantEvents>(sp => sp.GetRequiredService<ISignal>());

            services.AddScoped<ITagCache, DefaultTagCache>();
            services.AddScoped<ICacheContextManager, CacheContextManager>();
            services.AddScoped<ICacheScopeManager, CacheScopeManager>();

            services.AddScoped<ICacheContextProvider, FeaturesCacheContextProvider>();
            services.AddScoped<ICacheContextProvider, QueryCacheContextProvider>();
            services.AddScoped<ICacheContextProvider, RolesCacheContextProvider>();
            services.AddScoped<ICacheContextProvider, RouteCacheContextProvider>();
            services.AddScoped<ICacheContextProvider, UserCacheContextProvider>();
            services.AddScoped<ICacheContextProvider, KnownValueCacheContextProvider>();

            // IMemoryCache is registered at the tenant level so that there is one instance for each tenant.
            services.AddSingleton<IMemoryCache, MemoryCache>();

            // MemoryDistributedCache needs to be registered as a singleton as it owns a MemoryCache instance.
            services.AddSingleton<IDistributedCache, MemoryDistributedCache>();
        });

        // Adds services to keep in sync any document type between a document store and a multi level cache.
        return builder.AddDocumentManagement();
    }
}
