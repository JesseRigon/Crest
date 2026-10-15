using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Taxonomies.Fields;

namespace Grove.Taxonomies.ViewModels;

/// <summary>A term as a display shows it: its text and its link (relative to the shell's base).</summary>
public sealed record TaxonomyTermLink(string Text, string Url);

public class DisplayTaxonomyFieldViewModel
{
    public string TaxonomyContentItemId => Field.TaxonomyContentItemId;
    public string[] TermContentItemIds => Field.TermContentItemIds;
    /// <summary>The terms resolved by the driver (text and link), so the display tree carries them.</summary>
    public IReadOnlyList<TaxonomyTermLink> Items { get; set; } = [];

    public string Label => PartFieldDefinition?.DisplayName();

    public TaxonomyField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
