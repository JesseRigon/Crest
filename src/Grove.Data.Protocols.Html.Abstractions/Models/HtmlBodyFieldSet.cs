using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Data.Protocols.Html.Models;

/// <summary>
/// An item's HTML body. Its data is a field (<c>HtmlBody.Html</c>, an HTML field); this class is the
/// typed accessor over it. Sanitizing and checking its Liquid are the module's system flows, and the
/// rendered body a read projection (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class HtmlBodyFieldSet : ContentFieldSet
{
    public const string HtmlField = "Html";

    [JsonIgnore]
    public string Html
    {
        get => this.Get<Grove.Contents.Fields.Fields.HtmlField>(HtmlField)?.Html;
        set => this.Alter<Grove.Contents.Fields.Fields.HtmlField>(HtmlField, html => html.Html = value);
    }
}
