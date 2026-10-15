using Grove.Contents.Management;
using Grove.Modules;

namespace Grove.Contents.Fields.Fields;

[RequireFeatures("Grove.Contents.Localization")]
public class LocalizationSetContentPickerField : ContentField
{
    public string[] LocalizationSets { get; set; } = [];
}
