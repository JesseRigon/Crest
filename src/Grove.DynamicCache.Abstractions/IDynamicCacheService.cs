using Grove.Environment.Cache;

namespace Grove.DynamicCache;

public interface IDynamicCacheService : ITagRemovedEventHandler
{
    Task<string> GetCachedValueAsync(CacheContext context);
    Task SetCachedValueAsync(CacheContext context, string value);
}
