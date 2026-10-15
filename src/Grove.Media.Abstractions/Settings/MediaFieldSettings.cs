using System.ComponentModel;
using Grove.Contents.Management.Metadata.Settings;

namespace Grove.Media.Settings;

public class MediaFieldSettings : FieldSettings
{
    [DefaultValue(true)]
    public bool Multiple { get; set; } = true;

    [DefaultValue(true)]
    public bool AllowMediaText { get; set; } = true;

    public bool AllowAnchors { get; set; }

    public string[] AllowedExtensions { get; set; } = [];
}
