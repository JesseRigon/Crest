using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveSplitter.Expand" /> or <see cref="GroveSplitter.Collapse" /> event that is being raised.
/// </summary>
public class GroveSplitterEventArgs
{
    /// <summary>
    /// Gets the index of the pane.
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// Gets the pane which the event applies to.
    /// </summary>
    /// <value>The pane.</value>
    public GroveSplitterPane? Pane { get; set; }

    /// <summary>
    /// Gets or sets a value which will cancel the event.
    /// </summary>
    /// <value><c>true</c> to cancel the event; otherwise, <c>false</c>.</value>
    public bool Cancel { get; set; }
}

