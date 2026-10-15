using System.Reflection;

namespace Grove.Workflows.BlazorWasm;

/// <summary>
/// The assemblies loaded on demand for Studio: grove-lazy-assemblies.txt, embedded. The
/// same embedded file tells the host app's WASM build (Grove.LazyModules) which assemblies
/// to leave out of its startup download.
/// </summary>
public static class StudioAssemblies
{
    public static IReadOnlyList<string> Names { get; } = Read();

    private static string[] Read()
    {
        using var stream = typeof(StudioAssemblies).Assembly.GetManifestResourceStream("grove-lazy-assemblies.txt")
            ?? throw new InvalidOperationException("grove-lazy-assemblies.txt is not embedded in Grove.Workflows.BlazorWasm.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
