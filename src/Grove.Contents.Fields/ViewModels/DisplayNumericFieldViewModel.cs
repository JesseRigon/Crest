using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.ViewModels;

public class DisplayNumericFieldViewModel
{
    public decimal? Value => Field.Value;
    public NumericField Field { get; set; }
    public ContentFieldSet FieldSet { get; set; }
    public ContentFieldDefinition FieldDefinition { get; set; }

    /// <summary>The field's label, for the display component (docs/blazor-display.md rework step 7).</summary>
    public string Label => FieldDefinition?.DisplayName();

    /// <summary>The field's CSS name (<c>field-name-{part}-{field}</c>), for the display component.</summary>
    public string FieldClass => FieldDefinition is null ? null : Grove.Utilities.StringExtensions.HtmlClassify(FieldDefinition.FieldSetDefinition.Name + "-" + FieldDefinition.Name);
}
