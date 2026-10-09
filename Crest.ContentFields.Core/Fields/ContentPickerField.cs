using Crest.ContentManagement;

namespace Crest.ContentFields.Fields;

public class ContentPickerField : ContentField
{
    public string[] ContentItemIds { get; set; } = [];
}
