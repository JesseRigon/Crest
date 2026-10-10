using System.ComponentModel;
using Crest.ContentManagement.Metadata.Settings;

namespace Crest.Markdown.Settings;

public class MarkdownFieldSettings : FieldSettings
{
    [DefaultValue(true)]
    public bool SanitizeHtml { get; set; } = true;

    public bool RenderLiquid { get; set; }
}
