using Grove.ContentManagement;
using Grove.Modules;

namespace Grove.ContentFields.Fields;

[RequireFeatures("Grove.ContentLocalization")]
public class LocalizationSetContentPickerField : ContentField
{
    public string[] LocalizationSets { get; set; } = [];
}
