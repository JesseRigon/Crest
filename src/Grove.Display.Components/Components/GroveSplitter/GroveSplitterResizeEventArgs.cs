namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Display.Components.Primitives.GroveSplitter.Resize" /> event that is being raised.
/// </summary>
public class GroveSplitterResizeEventArgs : GroveSplitterEventArgs
{
    /// <summary>
    /// The new size of the pane
    /// </summary>
    public double NewSize { get; set; }
}

