using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Alias.Models;

/// <summary>
/// An item's alias, its stable logical identifier. Its data is a field (<c>Alias.Alias</c>, a text
/// field); this class is the typed accessor over it. Its behaviour (generated from the attachment's
/// pattern, unique, and the alias cache) is the module's system flows (docs/content-engine.md ›
/// Behaviour is workflows).
/// </summary>
public class AliasFieldSet : ContentFieldSet
{
    // Maximum length that MySql can support in an index under utf8mb4 collation is 768,
    // minus 2 for the `DocumentId` integer (bigint size = 8 bytes = 2 character size),
    // minus 26 for the `ContentItemId` and 1 for the 'Published' and 'Latest' bools.
    // minus 4 to allow at least to add a new integer column.
    public const int MaxAliasLength = 735;

    /// <summary>The field holding the alias.</summary>
    public const string AliasField = "Alias";

    [JsonIgnore]
    public string Alias
    {
        get => this.Get<TextField>(AliasField)?.Text;
        set => this.Alter<TextField>(AliasField, text => text.Text = value);
    }
}
