using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class ContentPickerField : ContentField
{
    public string[] ContentItemIds { get; set; } = [];
}
