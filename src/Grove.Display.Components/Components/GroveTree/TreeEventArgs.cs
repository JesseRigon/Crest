namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Display.Components.Primitives.GroveTree.Change" /> event that is being raised.
/// </summary>
public class TreeEventArgs
{
    /// <summary>
    /// Gets the <see cref="Grove.Display.Components.Primitives.GroveTreeItem.Text" /> the selected GroveTreeItem.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets the <see cref="Grove.Display.Components.Primitives.GroveTreeItem.Value" /> the selected GroveTreeItem.
    /// </summary>
    public object? Value { get; set; }
}

