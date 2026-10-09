using System.ComponentModel;
using Crest.ContentManagement.Metadata.Settings;

namespace Crest.ContentFields.Settings;

public class UserPickerFieldSettings : FieldSettings
{
    public bool Multiple { get; set; }

    [DefaultValue(true)]
    public bool DisplayAllUsers { get; set; } = true;

    public string[] DisplayedRoles { get; set; } = [];

    public string Placeholder { get; set; } = string.Empty;
}
