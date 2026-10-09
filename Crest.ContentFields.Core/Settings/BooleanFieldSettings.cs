using Crest.ContentManagement.Metadata.Settings;

namespace Crest.ContentFields.Settings;

public class BooleanFieldSettings : FieldSettings
{
    public string Label { get; set; }

    public bool DefaultValue { get; set; }
}
