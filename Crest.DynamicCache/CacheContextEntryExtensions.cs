using System.Text;
using Crest.Environment.Cache;

namespace Crest.DynamicCache;

public static class CacheContextEntryExtensions
{
    public static string GetContextHash(this IEnumerable<CacheContextEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries.OrderBy(x => x.Key).ThenBy(x => x.Value))
        {
            var part = entry.Key + entry.Value;
            sb.Append(part);
        }

        return sb.ToString();
    }
}
