using Crest.ContentManagement.Metadata;
using Crest.Environment.Cache;

namespace Crest.ContentManagement.Cache;

public class ContentDefinitionCacheContextProvider : ICacheContextProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContentDefinitionCacheContextProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task PopulateContextEntriesAsync(IEnumerable<string> contexts, List<CacheContextEntry> entries)
    {
        if (contexts.Any(ctx => string.Equals(ctx, "types", StringComparison.OrdinalIgnoreCase)))
        {
            var identifier = await _contentDefinitionManager.GetIdentifierAsync();
            entries.Add(new CacheContextEntry("types", identifier));

            return;
        }
    }
}
