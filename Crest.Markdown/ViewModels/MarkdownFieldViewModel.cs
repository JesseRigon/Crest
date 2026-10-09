using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Markdown.Fields;

namespace Crest.Markdown.ViewModels;

public class MarkdownFieldViewModel
{
    public string Markdown { get; set; }
    public string Html { get; set; }
    public MarkdownField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
