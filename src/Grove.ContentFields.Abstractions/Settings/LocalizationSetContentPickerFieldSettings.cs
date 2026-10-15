using Grove.ContentManagement.Metadata.Settings;
using Grove.Modules;

namespace Grove.ContentFields.Settings;

[RequireFeatures("Grove.ContentLocalization")]
public class LocalizationSetContentPickerFieldSettings : FieldSettings
{
    public bool Multiple { get; set; }

    public string[] DisplayedContentTypes { get; set; } = [];
}
