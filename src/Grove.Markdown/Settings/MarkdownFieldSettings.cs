using System.ComponentModel;
using Grove.ContentManagement.Metadata.Settings;

namespace Grove.Markdown.Settings;

public class MarkdownFieldSettings : FieldSettings
{
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;

    public bool RenderLiquid { get; set; }
}
