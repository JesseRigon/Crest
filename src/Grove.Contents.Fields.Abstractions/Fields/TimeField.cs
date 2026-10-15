using Grove.Contents.Management;

namespace Grove.Contents.Fields.Fields;

public class TimeField : ContentField
{
    public TimeSpan? Value { get; set; }
}
