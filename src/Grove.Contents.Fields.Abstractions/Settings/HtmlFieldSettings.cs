using System.ComponentModel;
using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Fields.Settings;

public class HtmlFieldSettings : FieldSettings
{
    /// <summary>
    /// Whether to sanitize the Html input.
    /// </summary>
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;

    /// <summary>
    /// Whether Liquid templating is enabled.
    /// </summary>
    public bool RenderLiquid { get; set; }
}
