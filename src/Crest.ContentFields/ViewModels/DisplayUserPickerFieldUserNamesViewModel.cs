using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class DisplayUserPickerFieldUserNamesViewModel
{
    public string[] UserIds => Field.UserIds;
    public string[] UserNames => Field.GetUserNames();
    public UserPickerField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
