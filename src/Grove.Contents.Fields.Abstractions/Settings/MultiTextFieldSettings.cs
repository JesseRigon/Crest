using System.Text.Json.Serialization;
using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Fields.Settings;

public class MultiTextFieldSettings : FieldSettings
{
    public MultiTextFieldValueOption[] Options { get; set; } = [];
}

public class MultiTextFieldValueOption
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("default")]
    public bool Default { get; set; }
}
