using Grove.ContentManagement.Metadata.Settings;

namespace Grove.ContentFields.Settings;

public class BooleanFieldSettings : FieldSettings
{
    public string Label { get; set; }

    public bool DefaultValue { get; set; }
}
