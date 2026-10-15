using Grove.Environment.Cache;

namespace Grove.Data.Cache.Dynamic;

public interface IDynamicCacheService : ITagRemovedEventHandler
{
    Task<string> GetCachedValueAsync(CacheContext context);
    Task SetCachedValueAsync(CacheContext context, string value);
}
