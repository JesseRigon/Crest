using Crest.ContentManagement.Metadata.Settings;
using Crest.Modules;

namespace Crest.ContentFields.Settings;

[RequireFeatures("Crest.ContentLocalization")]
public class LocalizationSetContentPickerFieldSettings : FieldSettings
{
    public bool Multiple { get; set; }

    public string[] DisplayedContentTypes { get; set; } = [];
}
