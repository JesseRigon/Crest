using Grove.ContentManagement;

namespace Grove.ContentFields.Fields;

public class YoutubeField : ContentField
{
    public string EmbeddedAddress { get; set; }
    public string RawAddress { get; set; }
}
