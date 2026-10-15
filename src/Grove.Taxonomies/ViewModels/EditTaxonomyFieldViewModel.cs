using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Taxonomies.Fields;

namespace Grove.Taxonomies.ViewModels;

public class EditTaxonomyFieldViewModel
{
    public string UniqueValue { get; set; }
    public List<TermEntry> TermEntries { get; set; } = [];

    [BindNever]
    public ContentItem Taxonomy { get; set; }

    [BindNever]
    public TaxonomyField Field { get; set; }

    [BindNever]
    public ContentPart Part { get; set; }

    [BindNever]
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}

public class TermEntry
{
    [BindNever]
    public ContentItem Term { get; set; }

    public bool Selected { get; set; }
    public string ContentItemId { get; set; }

    [BindNever]
    public int Level { get; set; }

    public bool IsLeaf { get; set; }
}
