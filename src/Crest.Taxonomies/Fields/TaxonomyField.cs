using Crest.ContentManagement;

namespace Crest.Taxonomies.Fields;

public class TaxonomyField : ContentField
{
    public string TaxonomyContentItemId { get; set; }
    public string[] TermContentItemIds { get; set; } = [];
}
