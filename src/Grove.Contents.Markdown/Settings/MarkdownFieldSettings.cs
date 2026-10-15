using System.ComponentModel;
using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Markdown.Settings;

public class MarkdownFieldSettings : FieldSettings
{
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;

    public bool RenderLiquid { get; set; }
}
