using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class DisplayContentPickerFieldViewModel
{
    public string[] ContentItemIds => Field.ContentItemIds;
    public ContentPickerField Field { get; set; }
    public ContentPart Part { get; set; }

    /// <summary>The picked items resolved for the viewer (text and link), so the display tree carries them (§ 7).</summary>
    public IReadOnlyList<DisplayLinkItem> Items { get; set; } = [];
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    /// <summary>The field's label, for the display component (docs/blazor-display.md rework step 7).</summary>
    public string Label => PartFieldDefinition?.DisplayName();

    /// <summary>The field's CSS name (<c>field-name-{part}-{field}</c>), for the display component.</summary>
    public string FieldClass => PartFieldDefinition is null ? null : Grove.Mvc.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);
}
