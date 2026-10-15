using System.ComponentModel;

namespace Grove.Markdown.Settings;

public class MarkdownBodyPartSettings
{
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;

    public bool RenderLiquid { get; set; }
}
