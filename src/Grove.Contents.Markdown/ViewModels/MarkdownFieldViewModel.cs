using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Markdown.Fields;

namespace Grove.Contents.Markdown.ViewModels;

public class MarkdownFieldViewModel
{
    public string Markdown { get; set; }
    public string Html { get; set; }
    public MarkdownField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
