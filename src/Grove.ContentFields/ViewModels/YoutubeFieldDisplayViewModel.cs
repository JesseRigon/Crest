using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class YoutubeFieldDisplayViewModel
{
    public string EmbeddedAddress => Field.EmbeddedAddress;
    public string RawAddress => Field.RawAddress;

    public int Width => PartFieldDefinition?.GetSettings<Settings.YoutubeFieldSettings>() is { Width: > 0 } settings ? settings.Width : 640;

    public int Height => PartFieldDefinition?.GetSettings<Settings.YoutubeFieldSettings>() is { Height: > 0 } settings ? settings.Height : 360;
    public YoutubeField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    /// <summary>The field's label, for the display component (docs/blazor-display.md rework step 7).</summary>
    public string Label => PartFieldDefinition?.DisplayName();

    /// <summary>The field's CSS name (<c>field-name-{part}-{field}</c>), for the display component.</summary>
    public string FieldClass => PartFieldDefinition is null ? null : Grove.Mvc.Utilities.StringExtensions.HtmlClassify(PartFieldDefinition.PartDefinition.Name + "-" + PartFieldDefinition.Name);
}
