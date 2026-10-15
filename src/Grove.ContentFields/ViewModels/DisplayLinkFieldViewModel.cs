using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class DisplayLinkFieldViewModel
{
    public string Url => Field.Url;

    /// <summary>The link's text as the field's settings choose it (static, the URL, or the item's text).</summary>
    public string LinkText
    {
        get
        {
            var settings = PartFieldDefinition?.GetSettings<Settings.LinkFieldSettings>();
            return settings?.LinkTextMode switch
            {
                Settings.LinkTextMode.Static => string.IsNullOrWhiteSpace(settings.DefaultText) ? Field.Url : settings.DefaultText,
                Settings.LinkTextMode.Url => Field.Url,
                Settings.LinkTextMode.Optional => string.IsNullOrWhiteSpace(Field.Text) ? Field.Url : Field.Text,
                _ => Field.Text,
            };
        }
    }
    public string Text => Field.Text;
    public string Target => Field.Target;
    public LinkField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    /// <summary>The field's label, for the display component (docs/blazor-display.md rework step 7).</summary>
    public string Label => PartFieldDefinition?.DisplayName();

    /// <summary>The field's CSS name (<c>field-name-{part}-{field}</c>), for the display component.</summary>
    public string FieldClass => PartFieldDefinition is null ? null : Grove.Mvc.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);
}
