using System.ComponentModel;

namespace Crest.Menu.Settings;

public class HtmlMenuItemPartSettings
{
    /// <summary>
    /// Whether to sanitize the html input.
    /// </summary>
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;
}
