using Grove.Contents.Management;

namespace Grove.Contents.Taxonomies.Fields;

public class TaxonomyField : ContentField
{
    public string TaxonomyContentItemId { get; set; }
    public string[] TermContentItemIds { get; set; } = [];
}
