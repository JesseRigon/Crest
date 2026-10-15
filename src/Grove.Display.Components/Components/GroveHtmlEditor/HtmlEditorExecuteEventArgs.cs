using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveHtmlEditor.Execute" /> event that is being raised.
/// </summary>
public class HtmlEditorExecuteEventArgs
{
    /// <summary>
    /// Gets the GroveHtmlEditor instance which raised the event.
    /// </summary>
    public GroveHtmlEditor Editor { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlEditorExecuteEventArgs"/> class.
    /// </summary>
    /// <param name="editor">The editor instance.</param>
    internal HtmlEditorExecuteEventArgs(GroveHtmlEditor editor)
    {
        Editor = editor;
    }

    /// <summary>
    /// Gets the name of the command which GroveHtmlEditor is executing.
    /// </summary>
    public string? CommandName { get; set; }
}

