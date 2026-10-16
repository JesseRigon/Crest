using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.ViewModels;

public class EditHtmlFieldViewModel
{
    public string Html { get; set; }
    public HtmlField Field { get; set; }
    public ContentFieldSet FieldSet { get; set; }
    public ContentFieldDefinition FieldDefinition { get; set; }
}
