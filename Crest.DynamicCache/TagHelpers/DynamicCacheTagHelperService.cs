using System.Collections.Concurrent;
using Microsoft.AspNetCore.Html;

namespace Crest.DynamicCache.TagHelpers;

public class DynamicCacheTagHelperService
{
    public ConcurrentDictionary<string, Task<IHtmlContent>> Workers = new();
}
