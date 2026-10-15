using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class EditHtmlFieldViewModel
{
    public string Html { get; set; }
    public HtmlField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
