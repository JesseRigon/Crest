using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.Models;

/// <summary>
/// A link menu item: its address and target (text fields). This class is the typed accessor over the fields (docs/content-engine.md › Behaviour is
/// workflows).
/// </summary>
public class LinkMenuItemFieldSet : ContentFieldSet
{
    /// <summary>
    /// The url of the link to create.
    /// </summary>
    [JsonIgnore]
    public string Url
    {
        get => this.Get<TextField>(nameof(Url))?.Text;
        set => this.Alter<TextField>(nameof(Url), fieldValue => fieldValue.Text = value);
    }

    /// <summary>
    /// The target of the link to create.
    /// </summary>
    [JsonIgnore]
    public string Target
    {
        get => this.Get<TextField>(nameof(Target))?.Text;
        set => this.Alter<TextField>(nameof(Target), fieldValue => fieldValue.Text = value);
    }
}
