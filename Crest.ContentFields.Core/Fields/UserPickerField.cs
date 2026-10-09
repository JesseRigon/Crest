using Crest.ContentManagement;

namespace Crest.ContentFields.Fields;

public class UserPickerField : ContentField
{
    public string[] UserIds { get; set; } = [];
}
