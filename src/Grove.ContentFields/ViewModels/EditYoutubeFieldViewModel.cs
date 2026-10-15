using System.ComponentModel.DataAnnotations;
using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentFields.ViewModels;

public class EditYoutubeFieldViewModel
{
    [DataType(DataType.Url, ErrorMessage = "The field only accepts Urls")]
    public string RawAddress { get; set; }

    public string EmbeddedAddress { get; set; }
    public YoutubeField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
