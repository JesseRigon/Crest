using System.Collections.Concurrent;

namespace Grove.Display.Management;

internal static class DisplayManagerAlternatesFactory
{
    private static readonly ConcurrentDictionary<DisplayAlternatesCacheKey, string[]> s_displayCache = new();

    public static string[] GetDisplayAlternates(string actualShapeType, string modelTypeName)
    {
        var key = new DisplayAlternatesCacheKey(actualShapeType, modelTypeName);
        return s_displayCache.GetOrAdd(key, static k => [$"{k.ActualShapeType}__{k.ModelTypeName}"]);
    }


    private readonly record struct DisplayAlternatesCacheKey(
        string ActualShapeType,
        string ModelTypeName);

}
