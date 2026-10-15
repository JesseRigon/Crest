using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Fields.Settings;

public class BooleanFieldSettings : FieldSettings
{
    public string Label { get; set; }

    public bool DefaultValue { get; set; }
}
