using Crest.ContentManagement;

namespace Crest.ContentFields.Fields;

public class TimeField : ContentField
{
    public TimeSpan? Value { get; set; }
}
