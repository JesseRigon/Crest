using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class MultiTextField : ContentField
{
    public string[] Values { get; set; } = [];
}
