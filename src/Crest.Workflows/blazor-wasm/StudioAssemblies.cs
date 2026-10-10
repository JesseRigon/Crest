using System.Reflection;

namespace Crest.Workflows.BlazorWasm;

/// <summary>
/// The assemblies loaded on demand for Studio: crest-lazy-assemblies.txt, embedded. The
/// same embedded file tells the host app's WASM build (Crest.LazyModules) which assemblies
/// to leave out of its startup download.
/// </summary>
public static class StudioAssemblies
{
    public static IReadOnlyList<string> Names { get; } = Read();

    private static string[] Read()
    {
        using var stream = typeof(StudioAssemblies).Assembly.GetManifestResourceStream("crest-lazy-assemblies.txt")
            ?? throw new InvalidOperationException("crest-lazy-assemblies.txt is not embedded in Crest.Workflows.BlazorWasm.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
