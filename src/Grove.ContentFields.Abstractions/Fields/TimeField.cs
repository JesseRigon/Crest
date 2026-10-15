using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class TimeField : ContentField
{
    public TimeSpan? Value { get; set; }
}
