using System.ComponentModel;

namespace Grove.Contents.Markdown.Settings;

public class MarkdownBodyFieldSetSettings
{
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;

    public bool RenderLiquid { get; set; }
}
