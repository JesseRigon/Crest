namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Components.Primitives.GroveHtmlEditor.Paste" /> event that is being raised.
/// </summary>
public class HtmlEditorPasteEventArgs
{
    /// <summary>
    /// Gets or sets the HTML content that is pasted in GroveHtmlEditor. Use the setter to filter unwanted markup from the pasted value.
    /// </summary>
    /// <value>The HTML.</value>
    public string? Html { get; set; }
}

