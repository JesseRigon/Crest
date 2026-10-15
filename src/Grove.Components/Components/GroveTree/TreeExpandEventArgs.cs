using Microsoft.AspNetCore.Components;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveTree.Expand" /> event that is being raised.
/// </summary>
public class TreeExpandEventArgs
{
    /// <summary>
    /// Gets the <see cref="Grove.Components.Primitives.GroveTreeItem.Value" /> the expanded GroveTreeItem.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets the <see cref="Grove.Components.Primitives.GroveTreeItem.Text" /> the expanded GroveTreeItem.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the children of the expanded GroveTreeItem.
    /// </summary>
    public TreeItemSettings? Children { get; set; }
}

