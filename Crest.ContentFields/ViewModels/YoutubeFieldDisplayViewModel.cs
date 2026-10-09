using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class YoutubeFieldDisplayViewModel
{
    public string EmbeddedAddress => Field.EmbeddedAddress;
    public string RawAddress => Field.RawAddress;
    public YoutubeField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
