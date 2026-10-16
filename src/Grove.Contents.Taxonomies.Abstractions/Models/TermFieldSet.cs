using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Taxonomies.Models;

/// <summary>
/// The taxonomy a term belongs to (<c>Term.TaxonomyContentItemId</c>, a text field; this class is the
/// typed accessor over it). The taxonomy's system flow writes it on every term it saves.
/// </summary>
public class TermFieldSet : ContentFieldSet
{
    public const string TaxonomyContentItemIdField = "TaxonomyContentItemId";

    [JsonIgnore]
    public string TaxonomyContentItemId
    {
        get => this.Get<TextField>(TaxonomyContentItemIdField)?.Text;
        set => this.Alter<TextField>(TaxonomyContentItemIdField, text => text.Text = value);
    }
}
