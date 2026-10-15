using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class UserPickerField : ContentField
{
    public string[] UserIds { get; set; } = [];
}
