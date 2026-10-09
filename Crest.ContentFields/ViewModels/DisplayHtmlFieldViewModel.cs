using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Html.ViewModels;

namespace Crest.ContentFields.ViewModels;

public class DisplayHtmlFieldViewModel : HtmlViewModelBase
{
    public HtmlField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
