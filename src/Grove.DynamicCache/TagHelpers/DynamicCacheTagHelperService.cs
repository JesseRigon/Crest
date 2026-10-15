using System.Collections.Concurrent;
using Microsoft.AspNetCore.Html;

namespace Grove.DynamicCache.TagHelpers;

public class DynamicCacheTagHelperService
{
    public ConcurrentDictionary<string, Task<IHtmlContent>> Workers = new();
}
