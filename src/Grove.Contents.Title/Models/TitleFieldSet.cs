using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Title.Models;

/// <summary>
/// An item's title. Its data is a field (<c>Title.Title</c>, a text field); this class is the typed
/// accessor over it. Its behaviour, setting the display text from the title or the attachment's
/// pattern and requiring a title, is the module's system flows (docs/content-engine.md ›
/// Behaviour is workflows).
/// </summary>
public class TitleFieldSet : ContentFieldSet
{
    /// <summary>The field holding the title.</summary>
    public const string TitleField = "Title";

    [JsonIgnore]
    public string Title
    {
        get => this.Get<TextField>(TitleField)?.Text;
        set => this.Alter<TextField>(TitleField, text => text.Text = value);
    }
}
