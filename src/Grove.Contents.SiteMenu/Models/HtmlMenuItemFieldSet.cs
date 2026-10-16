using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.Models;

/// <summary>
/// An HTML menu item: its address and target (text fields) and its markup (an HTML field). This class is the typed accessor over the fields (docs/content-engine.md › Behaviour is
/// workflows).
/// </summary>
public class HtmlMenuItemFieldSet : ContentFieldSet
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

    /// <summary>
    /// The raw html to display for this link.
    /// </summary>
    [JsonIgnore]
    public string Html
    {
        get => this.Get<HtmlField>(nameof(Html))?.Html;
        set => this.Alter<HtmlField>(nameof(Html), fieldValue => fieldValue.Html = value);
    }
}
