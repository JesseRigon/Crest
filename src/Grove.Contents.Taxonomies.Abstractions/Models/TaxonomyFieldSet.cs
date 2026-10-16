using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Taxonomies.Models;

/// <summary>
/// A taxonomy's own values: the type its terms are created as (<c>Taxonomy.TermContentType</c>, a text
/// field; this class is the typed accessor over it). The terms are the taxonomy's
/// <see cref="TermsEmbedding"/> embedding (docs/content-engine.md › Embedded content items): a
/// nested, referenceable embedding, each term holding its child terms under the same name.
/// </summary>
public class TaxonomyFieldSet : ContentFieldSet
{
    /// <summary>The name of the taxonomy's terms embedding, and of every term's children.</summary>
    public const string TermsEmbedding = "Terms";

    public const string TermContentTypeField = "TermContentType";

    [JsonIgnore]
    public string TermContentType
    {
        get => this.Get<TextField>(TermContentTypeField)?.Text;
        set => this.Alter<TextField>(TermContentTypeField, text => text.Text = value);
    }

    /// <summary>The terms directly under a taxonomy or a term.</summary>
    public static IReadOnlyList<ContentItem> Terms(ContentItem taxonomyOrTerm) => taxonomyOrTerm.Embedded(TermsEmbedding);

    /// <summary>The terms array directly under a taxonomy's or a term's content; null when there is none.</summary>
    public static JsonArray TermsArray(JsonNode content) => content?[TermsEmbedding]?["ContentItems"] as JsonArray;
}
