using Grove.Contents.Management.Metadata.Settings;
using Grove.Modules;

namespace Grove.Contents.Fields.Settings;

[RequireFeatures("Grove.Contents.Localization")]
public class LocalizationSetContentPickerFieldSettings : FieldSettings
{
    public bool Multiple { get; set; }

    public string[] DisplayedContentTypes { get; set; } = [];
}
