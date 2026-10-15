using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Markdown.Fields;

namespace Grove.Markdown.ViewModels;

public class EditMarkdownFieldViewModel
{
    public string Markdown { get; set; }
    public MarkdownField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
