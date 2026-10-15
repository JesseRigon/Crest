using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Fields.ViewModels;

public class DisplayTextFieldViewModel
{
    public string Text => Field.Text;

    /// <summary>The heading level of the <c>Header</c> display mode.</summary>
    public string Level => PartFieldDefinition?.GetSettings<Settings.TextFieldHeaderDisplaySettings>()?.Level ?? "h2";
    public TextField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    /// <summary>The field's label, for the display component (docs/blazor-display.md rework step 7).</summary>
    public string Label => PartFieldDefinition?.DisplayName();

    /// <summary>The field's CSS name (<c>field-name-{part}-{field}</c>), for the display component.</summary>
    public string FieldClass => PartFieldDefinition is null ? null : Grove.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);
}
