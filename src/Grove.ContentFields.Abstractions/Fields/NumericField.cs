using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class NumericField : ContentField
{
    public decimal? Value { get; set; }
}
