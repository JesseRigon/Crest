using System.ComponentModel;

namespace Grove.Contents.Alias.Settings;

public enum AliasFieldSetOptions
{
    Editable,
    GeneratedDisabled,
}

public class AliasFieldSetSettings
{
    [DefaultValue("{{ Model.ContentItem.DisplayText | slugify }}")]
    public string Pattern { get; set; } = "{{ Model.ContentItem.DisplayText | slugify }}";

    /// <summary>
    /// Gets or sets whether a user can define a custom alias.
    /// </summary>
    [DefaultValue(AliasFieldSetOptions.Editable)]
    public AliasFieldSetOptions Options { get; set; } = AliasFieldSetOptions.Editable;
}
