using Crest.Environment.Cache;

namespace Crest.DynamicCache;

public interface IDynamicCacheService : ITagRemovedEventHandler
{
    Task<string> GetCachedValueAsync(CacheContext context);
    Task SetCachedValueAsync(CacheContext context, string value);
}
