#nullable enable

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The limits on building one display tree (ruling 2026-10-12, "Render limits"). An overrun
/// replaces the subtree with an error node and a diagnostic; the build is never retried, since
/// it is deterministic and would overrun again.
/// </summary>
public sealed class DisplayTreeOptions
{
    /// <summary>The wall-clock budget for one tree, the Razor/Liquid fallback renders included.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The most nodes one tree may hold.</summary>
    public int MaxNodes { get; set; } = 5_000;

    /// <summary>The deepest a node may sit below the root (a composite that places itself is capped here).</summary>
    public int MaxDepth { get; set; } = 64;

    /// <summary>The most characters of property JSON and markup one tree may carry.</summary>
    public long MaxSize { get; set; } = 4 * 1024 * 1024;

    /// <summary>The most Liquid statements one template evaluation may run (loops included).</summary>
    public int MaxTemplateSteps { get; set; } = 100_000;

    /// <summary>How deep composites may nest (a template placing a template placing ...); a cycle stops here at the latest.</summary>
    public int MaxTemplateDepth { get; set; } = 8;
}
