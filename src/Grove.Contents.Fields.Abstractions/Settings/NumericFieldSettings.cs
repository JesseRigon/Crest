using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Contents.Fields.Settings;

public class NumericFieldSettings : FieldSettings
{
    public int Scale { get; set; }

    public decimal? Minimum { get; set; }

    public decimal? Maximum { get; set; }

    public string Placeholder { get; set; }

    public string DefaultValue { get; set; }
}
