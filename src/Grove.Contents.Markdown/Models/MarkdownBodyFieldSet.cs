using System.Text.Json.Serialization;
using Grove.Contents.Management;
using Grove.Contents.Markdown.Fields;

namespace Grove.Contents.Markdown.Models;

/// <summary>
/// An item's Markdown body. Its data is a field (<c>MarkdownBody.Markdown</c>, a Markdown field);
/// this class is the typed accessor over it. Checking its Liquid is the module's system flow, and the
/// rendered body a read projection (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class MarkdownBodyFieldSet : ContentFieldSet
{
    public const string MarkdownField = "Markdown";

    [JsonIgnore]
    public string Markdown
    {
        get => this.Get<Fields.MarkdownField>(MarkdownField)?.Markdown;
        set => this.Alter<Fields.MarkdownField>(MarkdownField, markdown => markdown.Markdown = value);
    }
}
