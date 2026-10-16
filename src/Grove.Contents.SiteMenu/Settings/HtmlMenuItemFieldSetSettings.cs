using System.ComponentModel;

namespace Grove.Contents.SiteMenu.Settings;

public class HtmlMenuItemFieldSetSettings
{
    /// <summary>
    /// Whether to sanitize the html input.
    /// </summary>
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;
}
