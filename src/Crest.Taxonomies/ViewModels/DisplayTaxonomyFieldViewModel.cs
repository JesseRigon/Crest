using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;
using Crest.Taxonomies.Fields;

namespace Crest.Taxonomies.ViewModels;

public class DisplayTaxonomyFieldViewModel
{
    public string TaxonomyContentItemId => Field.TaxonomyContentItemId;
    public string[] TermContentItemIds => Field.TermContentItemIds;
    public TaxonomyField Field { get; set; }
    public ContentPart Part { get; set; }
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
