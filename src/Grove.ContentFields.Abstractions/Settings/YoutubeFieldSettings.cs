using Grove.ContentManagement.Metadata.Settings;

namespace Grove.ContentFields.Settings;

public class YoutubeFieldSettings : FieldSettings
{
    public string Label { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string Placeholder { get; set; } = string.Empty;
}
