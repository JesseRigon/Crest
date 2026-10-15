using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class LinkField : ContentField
{
    public string Url { get; set; }

    public string Text { get; set; }

    public string Target { get; set; }
}
