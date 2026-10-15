using Microsoft.Extensions.Caching.Distributed;

namespace Grove.Data.Cache.Dynamic;

public interface IDynamicCache
{
    Task<byte[]> GetAsync(string key);
    Task RemoveAsync(string key);
    Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options);
}
