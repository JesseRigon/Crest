using System.Collections.Concurrent;
using Grove.Display.Management.Utilities;

namespace Grove.Contents.Taxonomies;

internal static class TermAlternatesFactory
{
    private static readonly ConcurrentDictionary<TermFieldSetAlternatesCacheKey, string[]> s_termFieldSetCache = new();
    private static readonly ConcurrentDictionary<TermAlternatesCacheKey, string[]> s_termCache = new();

    public static string[] GetTermFieldSetAlternates(string contentType, string displayType)
    {
        var key = new TermFieldSetAlternatesCacheKey(contentType ?? string.Empty, displayType ?? string.Empty);

        return s_termFieldSetCache.GetOrAdd(key, static k =>
        [
            $"Term_{k.DisplayType}",
            $"{k.ContentType}__Term",
            $"{k.ContentType}_{k.DisplayType}__Term"
        ]);
    }

    public static string[] GetTermAlternates(string differentiator, string termContentType)
    {
        var key = new TermAlternatesCacheKey(differentiator ?? string.Empty, termContentType ?? string.Empty);

        return s_termCache.GetOrAdd(key, static k =>
        {
            var encodedContentType = k.TermContentType.EncodeAlternateElement();

            if (string.IsNullOrEmpty(k.Differentiator))
            {
                return ["Term__" + encodedContentType];
            }

            return
            [
                "Term__" + k.Differentiator,
                "Term__" + encodedContentType,
            ];
        });
    }

    private readonly record struct TermFieldSetAlternatesCacheKey(
        string ContentType,
        string DisplayType);

    private readonly record struct TermAlternatesCacheKey(
        string Differentiator,
        string TermContentType);
}
